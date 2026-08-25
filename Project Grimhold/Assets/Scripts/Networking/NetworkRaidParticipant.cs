using Fusion;
using UnityEngine;

/// <summary>
/// Persistent-in-session identity for one admitted raid player.
///
/// This is the Fusion PlayerObject. State Authority owns its terminal result and
/// its current avatar reference; the avatar remains a separate network object so a
/// defeated body can remain in the raid after its owner returns to Town.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkRaidParticipant : NetworkBehaviour, IInputAuthorityGained
{
    [Networked]
    public NetworkString<_32> ProfileId { get; private set; }

    [Networked]
    public RaidParticipantId RaidParticipantId { get; private set; }

    /// <summary>Identifies the runner-scoped raid generation that owns this participant.</summary>
    [Networked]
    public NetworkString<_32> RaidGenerationId { get; private set; }

    [Networked]
    public NetworkString<_64> LoadoutReservationId { get; private set; }

    [Networked]
    public RaidParticipantState State { get; private set; }

    [Networked]
    public NetworkId CurrentAvatarId { get; private set; }

    [Networked]
    public int ResultSequence { get; private set; }

    [Networked]
    public NetworkBool IsExtractionCommitConfirmed { get; private set; }

    [Networked]
    public NetworkBool IsReturnAuthorized { get; private set; }

    /// <summary>
    /// Resolves the current avatar without changing simulation state.
    /// </summary>
    public bool TryResolveCurrentAvatar(out NetworkObject avatar)
    {
        avatar = null;
        return Runner != null && CurrentAvatarId.IsValid &&
            Runner.TryFindObject(CurrentAvatarId, out avatar) && avatar != null;
    }

    /// <summary>
    /// Initializes this participant during the State Authority spawn callback.
    /// </summary>
    internal void Initialize(
        string profileId,
        RaidParticipantId raidParticipantId,
        string raidGenerationId = null,
        string loadoutReservationId = null)
    {
        if (!raidParticipantId.IsValid)
        {
            throw new System.ArgumentException("Raid participant identity must be valid.", nameof(raidParticipantId));
        }

        ProfileId = profileId;
        RaidParticipantId = raidParticipantId;
        RaidGenerationId = raidGenerationId ?? string.Empty;
        LoadoutReservationId = loadoutReservationId ?? string.Empty;
        State = RaidParticipantState.Raiding;
        CurrentAvatarId = default;
        ResultSequence = 0;
        IsExtractionCommitConfirmed = false;
        IsReturnAuthorized = false;
    }

    /// <summary>
    /// Associates the only controllable avatar with this participant.
    /// </summary>
    internal bool TrySetCurrentAvatar(NetworkObject avatar)
    {
        if (!HasStateAuthority || State != RaidParticipantState.Raiding || avatar == null || !avatar.Id.IsValid)
        {
            return false;
        }

        if (CurrentAvatarId.IsValid)
        {
            return CurrentAvatarId == avatar.Id;
        }

        CurrentAvatarId = avatar.Id;
        return true;
    }

    /// <summary>
    /// Completes the defeated result only after the avatar has produced its lootable body.
    /// </summary>
    internal bool TryMarkDefeated(NetworkObject avatar)
    {
        if (!HasStateAuthority || !IsCurrentRaidingAvatar(avatar))
        {
            return false;
        }

        State = RaidParticipantState.Defeated;
        CurrentAvatarId = default;
        ResultSequence++;
        return true;
    }

    /// <summary>
    /// Records extraction. TASK-80 must subsequently confirm the matching sequence before return is allowed.
    /// </summary>
    internal bool TryMarkExtracted(NetworkObject avatar)
    {
        if (!HasStateAuthority || !IsCurrentRaidingAvatar(avatar))
        {
            return false;
        }

        State = RaidParticipantState.Extracted;
        ResultSequence++;
        return true;
    }

    /// <summary>
    /// Marks an active participant as aborted during an authoritative raid-wide close.
    /// No inventory or stash operation is performed by this transition.
    /// </summary>
    internal bool TryAbortForClosure()
    {
        return TryTransitionToAborted(authorizeReturn: true);
    }

    public void InputAuthorityGained()
    {
        Runner?.GetComponent<NetworkSpawnManager>()?
            .NotifyHostMigrationAuthorityChanged();
    }

    /// <summary>
    /// Terminalizes a raiding participant that did not recover a peer during Host Migration.
    /// This is not a voluntary Return and therefore never publishes Return authorization.
    /// </summary>
    internal bool TryAbortForHostMigrationRecovery()
    {
        return TryTransitionToAborted(authorizeReturn: false);
    }

    /// <summary>
    /// Called by TASK-80 after an idempotent local Loadout commit has been acknowledged.
    /// </summary>
    internal bool TryConfirmExtractionCommit(int resultSequence)
    {
        if (!HasStateAuthority || State != RaidParticipantState.Extracted ||
            resultSequence != ResultSequence || IsExtractionCommitConfirmed)
        {
            return false;
        }

        IsExtractionCommitConfirmed = true;
        return true;
    }

    /// <summary>
    /// Applies the dynamic-object remap produced by Host Migration restoration.
    /// </summary>
    internal void SetRestoredCurrentAvatar(NetworkId avatarId)
    {
        if (HasStateAuthority)
        {
            CurrentAvatarId = avatarId;
        }
    }

    /// <summary>
    /// Requests an authoritative abandonment. This is a discrete request, never a simulation tick RPC.
    /// </summary>
    public void RequestAbandon()
    {
        if (HasInputAuthority)
        {
            RPC_RequestAbandon();
        }
    }

    /// <summary>
    /// Requests permission to leave the raid after a terminal participant result.
    /// </summary>
    public void RequestReturn()
    {
        if (HasInputAuthority)
        {
            Debug.Log(
                $"[RAID-SPECTATOR] NetworkRaidParticipant.RequestReturn. " +
                $"State={State}, IsServer={Runner != null && Runner.IsServer}.",
                this);
            RPC_RequestReturn();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestAbandon(RpcInfo info = default)
    {
        if (State != RaidParticipantState.Raiding)
        {
            return;
        }

        NetworkSpawnManager spawnManager = Runner != null
            ? Runner.GetComponent<NetworkSpawnManager>()
            : null;
        string rejectionReason = null;
        if (spawnManager == null ||
            !spawnManager.TryResolveReturnRequester(
                this,
                info.Source,
                out bool requesterIsHost,
                out rejectionReason) ||
            requesterIsHost)
        {
            Debug.LogWarning(
                $"[HM-MULTI] Abandon rejected for the operational Host or an invalid requester. " +
                $"Reason={rejectionReason ?? "Operational Host cannot abandon during an active Raid"}.",
                this);
            return;
        }

        State = RaidParticipantState.Aborted;
        CurrentAvatarId = default;
        ResultSequence++;
        IsReturnAuthorized = true;
        Debug.Log($"[HM-MULTI] Client abandon authorized. State={State}.", this);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestReturn(RpcInfo info = default)
    {
        if (IsReturnAuthorized || State == RaidParticipantState.Raiding)
        {
            return;
        }

        if (State == RaidParticipantState.Extracted && !IsExtractionCommitConfirmed)
        {
            return;
        }

        NetworkSpawnManager spawnManager = Runner != null
            ? Runner.GetComponent<NetworkSpawnManager>()
            : null;
        string rejectionReason = null;
        if (spawnManager == null ||
            !spawnManager.TryResolveReturnRequester(
                this,
                info.Source,
                out bool requesterIsHost,
                out rejectionReason))
        {
            Debug.LogWarning(
                $"[HM-MULTI] Return rejected because requester identity could not be resolved. " +
                $"Reason={rejectionReason ?? "Missing NetworkSpawnManager"}.",
                this);
            return;
        }

        if (requesterIsHost)
        {
            Debug.LogWarning(
                "[HM-MULTI] Operational Host Return rejected while the Host must sustain the Raid.",
                this);
            return;
        }

        if (State == RaidParticipantState.Defeated)
        {
            if (!spawnManager.TryRegisterControlledReturn(this, out rejectionReason))
            {
                Debug.LogWarning(
                    $"[RAID-SPECTATOR] Client Return rejected. Reason={rejectionReason}.",
                    this);
                return;
            }
        }

        IsReturnAuthorized = true;
    }

    private bool IsCurrentRaidingAvatar(NetworkObject avatar)
    {
        return State == RaidParticipantState.Raiding && avatar != null &&
            avatar.Id.IsValid && avatar.Id == CurrentAvatarId;
    }

    private bool TryTransitionToAborted(bool authorizeReturn)
    {
        if (!HasStateAuthority || State != RaidParticipantState.Raiding)
        {
            return false;
        }

        State = RaidParticipantState.Aborted;
        CurrentAvatarId = default;
        ResultSequence++;
        IsReturnAuthorized = authorizeReturn;
        return true;
    }
}
