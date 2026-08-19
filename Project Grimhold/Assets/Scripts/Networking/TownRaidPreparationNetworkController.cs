using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Replicated state for exactly one Town Raid preparation. Membership identity is ProfileId;
/// PlayerRef is resolved only when routing requests and targeted release messages.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class TownRaidPreparationNetworkController : NetworkBehaviour, IStateAuthorityChanged
{
    private const int AuthorityReleaseRebuildDelayTicks = 3;
    private struct MemberNetwork : INetworkStruct
    {
        public NetworkString<_32> ProfileId;
        public NetworkBool IsReady;
        public int LaunchRevision;
        public NetworkBool LaunchAcknowledged;

        public bool IsOccupied => !string.IsNullOrWhiteSpace(ProfileId.ToString());
    }

    [SerializeField, Min(1f)]
    private float _coordinatedReleaseTimeoutSeconds = 15f;

    [Networked]
    public NetworkId DirectoryId { get; private set; }

    [Networked]
    public NetworkString<_8> RaidCodeValue { get; private set; }

    [Networked]
    public NetworkString<_32> HostProfileIdValue { get; private set; }

    [Networked]
    public TownRaidPreparationState State { get; private set; }

    [Networked]
    public int SnapshotRevision { get; private set; }

    [Networked]
    public int LaunchRevision { get; private set; }

    [Networked]
    public int FrozenMemberCount { get; private set; }

    [Networked]
    public int ReleaseRevision { get; private set; }

    [Networked, Capacity(RaidSessionRules.MaxParticipants)]
    private NetworkArray<MemberNetwork> Members => default;

    private readonly HashSet<ProfileId> _expectedDepartures = new();
    private readonly HashSet<ProfileId> _releasedProfiles = new();
    private readonly HashSet<ProfileId> _departedProfiles = new();
    private TownRaidPreparationDirectory _directory;
    private int _observedSnapshotRevision = -1;
    private int _acknowledgedLocalRevision;
    private int _rejectedLocalRevision;
    private bool _cancelLaunchRequested;
    private bool _releaseDispatched;
    private bool _hostReleaseRequested;
    private float _releaseDeadline;
    private float _acknowledgeDeadline;
    private NetworkId _initialDirectoryId;
    private RaidCode _initialRaidCode;
    private ProfileId _initialHostProfileId;
    private bool _hasSpawnInitialization;
    private int _authorityRebuildTicks;

    public TownRaidPreparationSnapshot Snapshot => BuildSnapshot();
    public RaidCode RaidCode => RaidCode.TryParse(RaidCodeValue.ToString(), out RaidCode code) ? code : default;
    public ProfileId HostProfileId => new(HostProfileIdValue.ToString());
    public int MemberCount => CountMembers();

    public override void Spawned()
    {
        ApplySpawnInitialization();
        ResolveDirectory();
        _observedSnapshotRevision = SnapshotRevision;
        _directory?.RegisterPreparation(this);
        TryMaterializeLocalLaunchContext();
    }

    public override void Render()
    {
        if (_observedSnapshotRevision == SnapshotRevision)
        {
            return;
        }

        _observedSnapshotRevision = SnapshotRevision;
        ResolveDirectory();
        _directory?.NotifyPreparationChanged(this);
        TryMaterializeLocalLaunchContext();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (_authorityRebuildTicks > 0)
        {
            _authorityRebuildTicks--;
            if (_authorityRebuildTicks == 0 && State == TownRaidPreparationState.Starting &&
                TownRaidPreparationRules.AreAllLaunchAcknowledged(Snapshot))
            {
                PrepareCoordinatedRelease(Snapshot);
            }
        }

        // A launch revision that never collects every ACK must not wait forever. The release
        // deadline only covers the phase after PrepareCoordinatedRelease, so the ACK phase owns
        // its own deadline inside the same coordinated preparation.
        if (State == TownRaidPreparationState.Starting && !_releaseDispatched &&
            _acknowledgeDeadline > 0f && Time.time >= _acknowledgeDeadline)
        {
            _cancelLaunchRequested = true;
            _acknowledgeDeadline = 0f;
        }

        if (State == TownRaidPreparationState.Starting && _releaseDispatched && !_hostReleaseRequested &&
            _releaseDeadline > 0f && Time.time >= _releaseDeadline)
        {
            _cancelLaunchRequested = true;
            _releaseDeadline = 0f;
        }

        if (!_cancelLaunchRequested)
        {
            return;
        }

        _cancelLaunchRequested = false;
        if (State == TownRaidPreparationState.Starting)
        {
            CancelLaunchingPreparation();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _directory?.UnregisterPreparation(this);
        _directory = null;
        ResetLaunchRuntime();
    }

    public void StateAuthorityChanged()
    {
        if (HasStateAuthority && State == TownRaidPreparationState.Starting)
        {
            ResetReleaseRuntime();
            ArmAcknowledgeDeadline();
            // Directory routing rebuilds in two ticks; release waits one additional tick.
            _authorityRebuildTicks = AuthorityReleaseRebuildDelayTicks;
        }
    }

    /// <summary>
    /// Captures validated initialization during Fusion's pre-spawn callback. Network authority
    /// properties are intentionally not consulted until <see cref="Spawned"/>.
    /// </summary>
    public bool TrySetSpawnInitialization(
        NetworkRunner runner,
        NetworkObject expectedObject,
        NetworkId directoryId,
        RaidCode raidCode,
        ProfileId hostProfileId)
    {
        if (_hasSpawnInitialization || runner == null || expectedObject == null ||
            expectedObject.gameObject != gameObject || expectedObject.GetComponent<TownRaidPreparationNetworkController>() != this ||
            directoryId.Raw == 0 || !raidCode.IsValid || !hostProfileId.IsValid)
        {
            return false;
        }

        _initialDirectoryId = directoryId;
        _initialRaidCode = raidCode;
        _initialHostProfileId = hostProfileId;
        _hasSpawnInitialization = true;
        return true;
    }

    private void ApplySpawnInitialization()
    {
        if (!HasStateAuthority || !_hasSpawnInitialization)
        {
            return;
        }

        DirectoryId = _initialDirectoryId;
        RaidCodeValue = _initialRaidCode.Value;
        HostProfileIdValue = _initialHostProfileId.Value;
        State = TownRaidPreparationState.Waiting;
        LaunchRevision = 0;
        FrozenMemberCount = 0;
        ReleaseRevision = 0;
        for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
        {
            Members.Set(index, default);
        }

        Members.Set(0, new MemberNetwork { ProfileId = _initialHostProfileId.Value, IsReady = false });
        SnapshotRevision = 1;
        _hasSpawnInitialization = false;
    }

    public bool AuthorityTryAddMember(ProfileId profileId)
    {
        if (!HasStateAuthority || !TownRaidPreparationRules.CanJoin(Snapshot, profileId))
        {
            return false;
        }

        int slot = FindEmptyMemberSlot();
        if (slot < 0)
        {
            return false;
        }

        Members.Set(slot, new MemberNetwork { ProfileId = profileId.Value, IsReady = false });
        MarkSnapshotChanged();
        return true;
    }

    public bool AuthorityTryRemoveMember(ProfileId profileId)
    {
        if (!HasStateAuthority || profileId == HostProfileId || !TownRaidPreparationRules.CanLeave(Snapshot, profileId))
        {
            return false;
        }

        int memberIndex = FindMember(profileId);
        if (memberIndex < 0)
        {
            return false;
        }

        for (int index = memberIndex; index < RaidSessionRules.MaxParticipants - 1; index++)
        {
            Members.Set(index, Members[index + 1]);
        }

        Members.Set(RaidSessionRules.MaxParticipants - 1, default);
        MarkSnapshotChanged();
        return true;
    }

    public bool AuthorityTrySetReady(ProfileId profileId, bool isReady)
    {
        if (!HasStateAuthority || !TownRaidPreparationRules.CanSetReady(Snapshot, profileId))
        {
            return false;
        }

        int memberIndex = FindMember(profileId);
        if (memberIndex < 0)
        {
            return false;
        }

        MemberNetwork member = Members[memberIndex];
        if ((bool)member.IsReady == isReady)
        {
            return true;
        }

        member.IsReady = isReady;
        Members.Set(memberIndex, member);
        MarkSnapshotChanged();
        return true;
    }

    public bool AuthorityTryStart(ProfileId requester)
    {
        TownRaidPreparationSnapshot snapshot = Snapshot;
        if (!HasStateAuthority || !TownRaidPreparationRules.CanStart(snapshot, requester))
        {
            return false;
        }

        int nextRevision = LaunchRevision + 1;
        if (!TownRaidPreparationRules.TryFreeze(snapshot, requester, nextRevision, out TownRaidPreparationSnapshot frozen))
        {
            return false;
        }

        LaunchRevision = nextRevision;
        FrozenMemberCount = frozen.FrozenMemberCount;
        State = TownRaidPreparationState.Starting;
        for (int index = 0; index < frozen.Members.Count; index++)
        {
            MemberNetwork member = Members[index];
            member.LaunchRevision = nextRevision;
            member.LaunchAcknowledged = false;
            Members.Set(index, member);
        }

        ArmAcknowledgeDeadline();
        MarkSnapshotChanged();
        TryMaterializeLocalLaunchContext();
        return true;
    }

    private void ArmAcknowledgeDeadline()
    {
        _acknowledgeDeadline = Time.time + Mathf.Max(1f, _coordinatedReleaseTimeoutSeconds);
    }

    public void AuthorityHandlePlayerLeft(ProfileId profileId)
    {
        if (!HasStateAuthority || State != TownRaidPreparationState.Starting || !profileId.IsValid)
        {
            return;
        }

        if (profileId == HostProfileId)
        {
            if (_hostReleaseRequested)
            {
                _directory?.AuthorityDissolvePreparation(this);
            }
            else
            {
                _cancelLaunchRequested = true;
            }

            return;
        }

        if (!_expectedDepartures.Contains(profileId) || !_releasedProfiles.Contains(profileId))
        {
            _cancelLaunchRequested = true;
            return;
        }

        _departedProfiles.Add(profileId);
        TryReleaseHostAfterRemoteDepartures();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_AcknowledgeLaunch(int sequence, RpcInfo info = default)
    {
        if (State != TownRaidPreparationState.Starting || sequence != LaunchRevision ||
            !TryResolveSender(info.Source, out ProfileId profileId))
        {
            return;
        }

        int memberIndex = FindMember(profileId);
        if (memberIndex < 0)
        {
            return;
        }

        MemberNetwork member = Members[memberIndex];
        if (member.LaunchRevision != sequence)
        {
            return;
        }

        if (!member.LaunchAcknowledged)
        {
            member.LaunchAcknowledged = true;
            Members.Set(memberIndex, member);
            MarkSnapshotChanged();
        }

        TownRaidPreparationSnapshot snapshot = Snapshot;
        if (!_releaseDispatched && TownRaidPreparationRules.AreAllLaunchAcknowledged(snapshot))
        {
            PrepareCoordinatedRelease(snapshot);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReleaseLaunch([RpcTarget] PlayerRef target, NetworkString<_8> raidCode, int sequence)
    {
        SessionConnectionCoordinator.Instance?.BeginAcknowledgedRaidLaunch(raidCode.ToString(), sequence);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CancelLaunch([RpcTarget] PlayerRef target, NetworkString<_8> raidCode, int sequence)
    {
        SessionConnectionCoordinator.Instance?.CancelPendingRaidLaunch(raidCode.ToString(), sequence);
    }

    private void TryMaterializeLocalLaunchContext()
    {
        TownRaidPreparationSnapshot snapshot = Snapshot;
        if (_acknowledgedLocalRevision == snapshot.LaunchRevision ||
            _rejectedLocalRevision == snapshot.LaunchRevision ||
            !TryGetLocalProfile(out ProfileId localProfile) ||
            !TownRaidPreparationRules.TryCreateLaunchContext(snapshot, localProfile, out RaidLaunchContext context))
        {
            return;
        }

        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator == null)
        {
            return;
        }

        RaidLaunchPreparationResult preparation = coordinator.TryStoreRaidLaunchContext(context);
        if (preparation == RaidLaunchPreparationResult.NotReady)
        {
            return;
        }

        if (preparation != RaidLaunchPreparationResult.Success ||
            !coordinator.HasActiveLaunchTicket(snapshot.RaidCode.Value, snapshot.LaunchRevision, localProfile))
        {
            RejectLocalLaunch(snapshot.LaunchRevision);
            return;
        }

        _acknowledgedLocalRevision = snapshot.LaunchRevision;
        RPC_AcknowledgeLaunch(snapshot.LaunchRevision);
    }

    /// <summary>
    /// Reports one permanent local preparation failure exactly once per launch revision, so a
    /// rejected revision never re-runs preparation or re-logs on later snapshots. The peer never
    /// decides the cohort outcome itself: the State Authority remains the only canceller.
    /// </summary>
    private void RejectLocalLaunch(int sequence)
    {
        if (sequence <= 0 || _rejectedLocalRevision == sequence)
        {
            return;
        }

        _rejectedLocalRevision = sequence;
        SessionConnectionCoordinator.Instance?.CancelPendingRaidLaunch(RaidCodeValue.ToString(), sequence);
        if (HasStateAuthority)
        {
            AuthorityHandleLaunchRejected(sequence);
            return;
        }

        RPC_RejectLaunch(sequence);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RejectLaunch(int sequence, RpcInfo info = default)
    {
        if (!TryResolveSender(info.Source, out ProfileId profileId) || FindMember(profileId) < 0)
        {
            return;
        }

        AuthorityHandleLaunchRejected(sequence);
    }

    private void AuthorityHandleLaunchRejected(int sequence)
    {
        if (!HasStateAuthority || State != TownRaidPreparationState.Starting || sequence != LaunchRevision)
        {
            return;
        }

        _cancelLaunchRequested = true;
    }

    private void PrepareCoordinatedRelease(in TownRaidPreparationSnapshot snapshot)
    {
        if (!HasStateAuthority || !TownRaidPreparationRules.AreAllLaunchAcknowledged(snapshot) ||
            snapshot.LaunchRevision != LaunchRevision)
        {
            return;
        }

        _expectedDepartures.Clear();
        _releasedProfiles.Clear();
        _departedProfiles.Clear();
        _releaseDispatched = true;
        bool resumingRelease = ReleaseRevision == snapshot.LaunchRevision;
        ReleaseRevision = snapshot.LaunchRevision;
        _releaseDeadline = Time.time + Mathf.Max(1f, _coordinatedReleaseTimeoutSeconds);
        _acknowledgeDeadline = 0f;

        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            ProfileId profileId = snapshot.Members[index].ProfileId;
            if (profileId == snapshot.HostProfileId)
            {
                continue;
            }

            if (_directory == null || !_directory.TryResolvePlayer(profileId, out PlayerRef player))
            {
                if (resumingRelease)
                {
                    _expectedDepartures.Add(profileId);
                    _releasedProfiles.Add(profileId);
                    _departedProfiles.Add(profileId);
                    continue;
                }

                _cancelLaunchRequested = true;
                return;
            }

            _expectedDepartures.Add(profileId);
            _releasedProfiles.Add(profileId);
            RPC_ReleaseLaunch(player, snapshot.RaidCode.Value, snapshot.LaunchRevision);
        }

        TryReleaseHostAfterRemoteDepartures();
    }

    private void TryReleaseHostAfterRemoteDepartures()
    {
        if (!_releaseDispatched || _hostReleaseRequested ||
            _departedProfiles.Count < _expectedDepartures.Count)
        {
            return;
        }

        if (_directory == null || !_directory.TryResolvePlayer(HostProfileId, out PlayerRef hostPlayer))
        {
            _cancelLaunchRequested = true;
            return;
        }

        _hostReleaseRequested = true;
        _releaseDeadline = 0f;
        RPC_ReleaseLaunch(hostPlayer, RaidCode.Value, LaunchRevision);
    }

    private void CancelLaunchingPreparation()
    {
        int sequence = LaunchRevision;
        TownRaidPreparationSnapshot snapshot = Snapshot;
        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            if (_directory != null && _directory.TryResolvePlayer(snapshot.Members[index].ProfileId, out PlayerRef player))
            {
                RPC_CancelLaunch(player, snapshot.RaidCode.Value, sequence);
            }
        }

        if (_directory == null || !_directory.AuthorityDissolvePreparation(this))
        {
            _cancelLaunchRequested = true;
        }
    }

    private TownRaidPreparationSnapshot BuildSnapshot()
    {
        var members = new List<TownRaidPreparationMember>(RaidSessionRules.MaxParticipants);
        for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
        {
            MemberNetwork member = Members[index];
            if (!member.IsOccupied)
            {
                break;
            }

            members.Add(new TownRaidPreparationMember(
                new ProfileId(member.ProfileId.ToString()),
                member.IsReady,
                member.LaunchRevision,
                member.LaunchAcknowledged));
        }

        return new TownRaidPreparationSnapshot(
            RaidCode,
            HostProfileId,
            State,
            members,
            SnapshotRevision,
            LaunchRevision,
            FrozenMemberCount);
    }

    private int CountMembers()
    {
        int count = 0;
        for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
        {
            if (!Members[index].IsOccupied)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private int FindMember(ProfileId profileId)
    {
        for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
        {
            MemberNetwork member = Members[index];
            if (!member.IsOccupied)
            {
                break;
            }

            if (string.Equals(member.ProfileId.ToString(), profileId.Value, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private int FindEmptyMemberSlot()
    {
        for (int index = 0; index < RaidSessionRules.MaxParticipants; index++)
        {
            if (!Members[index].IsOccupied)
            {
                return index;
            }
        }

        return -1;
    }

    private void MarkSnapshotChanged()
    {
        SnapshotRevision++;
        _observedSnapshotRevision = SnapshotRevision;
        ResolveDirectory();
        _directory?.NotifyPreparationChanged(this);
    }

    private void ResolveDirectory()
    {
        if (_directory != null || Runner == null || DirectoryId.Raw == 0 ||
            !Runner.TryFindObject(DirectoryId, out NetworkObject directoryObject) || directoryObject == null)
        {
            return;
        }

        directoryObject.TryGetBehaviour(out _directory);
    }

    private bool TryResolveSender(PlayerRef player, out ProfileId profileId)
    {
        profileId = default;
        NetworkObject playerObject = Runner != null && !player.IsNone ? Runner.GetPlayerObject(player) : null;
        if (playerObject == null || !playerObject.TryGetBehaviour(out SocialPlayerIdentity identity) || identity == null ||
            identity.Object.InputAuthority != player || string.IsNullOrWhiteSpace(identity.ProfileId.ToString()))
        {
            return false;
        }

        profileId = new ProfileId(identity.ProfileId.ToString());
        return true;
    }

    private bool TryGetLocalProfile(out ProfileId profileId)
    {
        profileId = default;
        LocalPlayerJoinContext context = Runner != null ? Runner.GetComponent<LocalPlayerJoinContext>() : null;
        if (context == null || !context.JoinData.ProfileId.IsValid)
        {
            return false;
        }

        profileId = context.JoinData.ProfileId;
        return true;
    }

    private void ResetLaunchRuntime()
    {
        _acknowledgedLocalRevision = 0;
        _rejectedLocalRevision = 0;
        _authorityRebuildTicks = 0;
        ResetReleaseRuntime();
    }

    private void ResetReleaseRuntime()
    {
        _expectedDepartures.Clear();
        _releasedProfiles.Clear();
        _departedProfiles.Clear();
        _cancelLaunchRequested = false;
        _releaseDispatched = false;
        _hostReleaseRequested = false;
        _releaseDeadline = 0f;
        _acknowledgeDeadline = 0f;
    }
}
