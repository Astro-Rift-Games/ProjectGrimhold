using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative Town boundary for creating and mutating concurrent Raid preparations.
/// Its code/profile indices are local projections of per-preparation replicated snapshots.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class TownRaidPreparationDirectory : NetworkBehaviour, IPlayerJoined, IPlayerLeft, IStateAuthorityChanged
{
    private const int AuthorityRebuildDelayTicks = 2;
    private const int RandomCodeAttempts = 128;

    [SerializeField]
    private NetworkPrefabRef _preparationPrefab;

    private readonly List<TownRaidPreparationNetworkController> _preparations = new();
    private readonly TownRaidPreparationDirectoryCache<TownRaidPreparationNetworkController> _cache = new();
    private readonly Dictionary<PlayerRef, ProfileId> _profileByPlayer = new();
    private readonly TownPartyContinuationClaimRegistry _continuationClaims = new();
    private bool _interactionRequested;
    private bool _indexReady;
    private bool _conflictLogged;
    private int _rebuildTicksRemaining;
    private int _observedContinuationClaimEpoch = -1;
    private TownPartyContinuationContext _observedLocalContinuation;
    private bool _continuationClaimRequested;
    private bool _continuationClaimAcknowledged;

    [Networked]
    public int ContinuationClaimEpoch { get; private set; }

    public event Action PreparationInteractionRequested;

    public bool IsIndexReady => _indexReady;
    public int PreparationCount => _preparations.Count;

    public bool RequestCreate() => CanSendOrdinaryPartyRequest && TrySend(RPC_RequestCreate());
    public bool RequestJoin(string code) =>
        CanSendOrdinaryPartyRequest && RaidCode.TryParse(code, out _) && TrySend(RPC_RequestJoin(code));
    public bool RequestLeave() => CanSendRequest && TrySend(RPC_RequestLeave());
    public bool RequestSetReady(bool isReady) => CanSendOrdinaryPartyRequest && TrySend(RPC_RequestSetReady(isReady));
    public bool RequestStart() => CanSendOrdinaryPartyRequest && TrySend(RPC_RequestStart());

    public bool RequestAbandonContinuation()
    {
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (!CanSendRequest || coordinator == null ||
            !coordinator.TryGetPendingPartyContinuation(out TownPartyContinuationContext context))
        {
            return false;
        }

        RpcInvokeInfo result = RPC_RequestAbandonContinuation(
            context.OriginRaidCode.Value,
            context.OriginLaunchRevision,
            context.HostProfileId.Value,
            context.Members.Count,
            context.Members[0].Value,
            context.Members.Count > 1 ? context.Members[1].Value : string.Empty);
        if (!TrySend(result))
        {
            return false;
        }

        coordinator.AbandonPartyContinuation(context);
        ResetLocalContinuationClaim();
        return true;
    }

    public void NotifyLocalInteractionRequested()
    {
        _interactionRequested = true;
    }

    public override void Spawned()
    {
        _indexReady = !HasStateAuthority;
        if (HasStateAuthority)
        {
            BeginAuthorityRebuild();
        }
    }

    public override void Render()
    {
        ObserveLocalContinuation();

        if (!_interactionRequested)
        {
            return;
        }

        _interactionRequested = false;
        PreparationInteractionRequested?.Invoke();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _rebuildTicksRemaining <= 0)
        {
            return;
        }

        _rebuildTicksRemaining--;
        if (_rebuildTicksRemaining == 0)
        {
            RebuildAuthorityIndices();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _interactionRequested = false;
        _indexReady = false;
        _preparations.Clear();
        _profileByPlayer.Clear();
        _continuationClaims.Clear();
        ResetLocalContinuationClaim();
    }

    public void StateAuthorityChanged()
    {
        if (HasStateAuthority)
        {
            BeginAuthorityRebuild();
        }
        else
        {
            _indexReady = true;
            _rebuildTicksRemaining = 0;
        }
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority || !_profileByPlayer.Remove(player, out ProfileId profileId) || !profileId.IsValid ||
            !_cache.TryResolve(profileId, out TownRaidPreparationNetworkController preparation))
        {
            return;
        }

        if (preparation.State == TownRaidPreparationState.Starting)
        {
            preparation.AuthorityHandlePlayerLeft(profileId);
            return;
        }

        if (profileId == preparation.HostProfileId)
        {
            AuthorityDissolvePreparation(preparation);
        }
        else
        {
            preparation.AuthorityTryRemoveMember(profileId);
        }
    }

    public bool TryGetPreparation(ProfileId profileId, out TownRaidPreparationNetworkController preparation)
    {
        preparation = null;
        return profileId.IsValid && _cache.TryResolve(profileId, out preparation);
    }

    public bool TryGetPreparation(RaidCode code, out TownRaidPreparationNetworkController preparation)
    {
        preparation = null;
        return code.IsValid && _cache.TryResolve(code, out preparation);
    }

    public bool TryResolvePlayer(ProfileId profileId, out PlayerRef player)
    {
        player = PlayerRef.None;
        foreach (KeyValuePair<PlayerRef, ProfileId> entry in _profileByPlayer)
        {
            if (entry.Value == profileId)
            {
                player = entry.Key;
                return true;
            }
        }

        if (Runner == null)
        {
            return false;
        }

        foreach (PlayerRef candidate in Runner.ActivePlayers)
        {
            if (TryResolveSender(candidate, out ProfileId candidateProfile))
            {
                _profileByPlayer[candidate] = candidateProfile;
                if (candidateProfile == profileId)
                {
                    player = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    public void RegisterPreparation(TownRaidPreparationNetworkController preparation)
    {
        if (preparation == null)
        {
            return;
        }

        if (!_preparations.Contains(preparation))
        {
            _preparations.Add(preparation);
        }

        UpdateCache(preparation);
    }

    public void NotifyPreparationChanged(TownRaidPreparationNetworkController preparation)
    {
        if (preparation != null && _preparations.Contains(preparation))
        {
            UpdateCache(preparation);
        }
    }

    public void UnregisterPreparation(TownRaidPreparationNetworkController preparation)
    {
        if (preparation == null)
        {
            return;
        }

        _preparations.Remove(preparation);
        _cache.Unregister(preparation);
        RefreshConflictState();
    }

    public bool AuthorityDissolvePreparation(TownRaidPreparationNetworkController preparation)
    {
        if (!CanMutate || preparation == null || preparation.Object == null || !preparation.Object.IsValid ||
            !preparation.HasStateAuthority)
        {
            return false;
        }

        UnregisterPreparation(preparation);
        Runner.Despawn(preparation.Object);
        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestCreate(RpcInfo info = default)
    {
        if (!CanMutate || !_preparationPrefab.IsValid || !TryResolveSender(info.Source, out ProfileId profileId) ||
            _cache.TryResolve(profileId, out _ ) || !TryGenerateUniqueRaidCode(out RaidCode raidCode))
        {
            return default;
        }

        _profileByPlayer[info.Source] = profileId;
        bool initialized = false;
        NetworkObject spawned = Runner.Spawn(
            _preparationPrefab,
            Vector3.zero,
            Quaternion.identity,
            null,
            (callbackRunner, networkObject) =>
            {
                if (networkObject.TryGetBehaviour(out TownRaidPreparationNetworkController preparation))
                {
                    initialized = preparation.TrySetSpawnInitialization(
                        callbackRunner,
                        networkObject,
                        Object.Id,
                        raidCode,
                        profileId);
                }
            });
        if (spawned == null || !initialized)
        {
            Debug.LogError($"{nameof(TownRaidPreparationDirectory)} failed to spawn a preparation.", this);
            if (spawned != null && spawned.IsValid)
            {
                Runner.Despawn(spawned);
            }
        }

        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestJoin(NetworkString<_8> requestedCode, RpcInfo info = default)
    {
        if (!CanMutate || !RaidCode.TryParse(requestedCode.ToString(), out RaidCode code) ||
            !TryResolveSender(info.Source, out ProfileId profileId) || _cache.TryResolve(profileId, out _) ||
            !_cache.TryResolve(code, out TownRaidPreparationNetworkController preparation) ||
            !preparation.HasStateAuthority || !preparation.AuthorityTryAddMember(profileId))
        {
            return default;
        }

        _profileByPlayer[info.Source] = profileId;
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestLeave(RpcInfo info = default)
    {
        if (!CanMutate || !TryResolveSender(info.Source, out ProfileId profileId) ||
            !_cache.TryResolve(profileId, out TownRaidPreparationNetworkController preparation) ||
            preparation.State != TownRaidPreparationState.Waiting)
        {
            return default;
        }

        _profileByPlayer[info.Source] = profileId;
        if (profileId == preparation.HostProfileId)
        {
            AuthorityDissolvePreparation(preparation);
        }
        else
        {
            preparation.AuthorityTryRemoveMember(profileId);
        }

        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestSetReady(NetworkBool isReady, RpcInfo info = default)
    {
        if (!CanMutate || !TryResolveSender(info.Source, out ProfileId profileId) ||
            !_cache.TryResolve(profileId, out TownRaidPreparationNetworkController preparation) ||
            !preparation.HasStateAuthority)
        {
            return default;
        }

        _profileByPlayer[info.Source] = profileId;
        preparation.AuthorityTrySetReady(profileId, isReady);
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestStart(RpcInfo info = default)
    {
        if (!CanMutate || !TryResolveSender(info.Source, out ProfileId profileId) ||
            !_cache.TryResolve(profileId, out TownRaidPreparationNetworkController preparation) ||
            !preparation.HasStateAuthority)
        {
            return default;
        }

        _profileByPlayer[info.Source] = profileId;
        preparation.AuthorityTryStart(profileId);
        return default;
    }

    private void BeginAuthorityRebuild()
    {
        _indexReady = false;
        _rebuildTicksRemaining = AuthorityRebuildDelayTicks;
        _continuationClaims.Clear();
    }

    private void RebuildAuthorityIndices()
    {
        var entries = new List<KeyValuePair<TownRaidPreparationNetworkController, TownRaidPreparationSnapshot>>(
            _preparations.Count);
        for (int index = 0; index < _preparations.Count; index++)
        {
            TownRaidPreparationNetworkController preparation = _preparations[index];
            if (preparation != null && preparation.Object != null && preparation.Object.IsValid)
            {
                entries.Add(new KeyValuePair<TownRaidPreparationNetworkController, TownRaidPreparationSnapshot>(
                    preparation,
                    preparation.Snapshot));
            }
        }

        _profileByPlayer.Clear();
        if (Runner != null)
        {
            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                if (TryResolveSender(player, out ProfileId profileId))
                {
                    _profileByPlayer[player] = profileId;
                }
            }
        }

        _indexReady = _cache.Rebuild(entries);
        if (_indexReady && ContinuationClaimEpoch < int.MaxValue)
        {
            ContinuationClaimEpoch++;
        }
        RefreshConflictState();
    }

    public void PlayerJoined(PlayerRef player)
    {
        _continuationClaimRequested = true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestRestoreContinuation(
        NetworkString<_8> originRaidCode,
        int originLaunchRevision,
        NetworkString<_32> hostProfileId,
        int memberCount,
        NetworkString<_32> firstMember,
        NetworkString<_32> secondMember,
        RpcInfo info = default)
    {
        if (!CanMutate || !TryResolveSender(info.Source, out ProfileId claimant) ||
            !TryDecodeContinuation(
                originRaidCode,
                originLaunchRevision,
                hostProfileId,
                memberCount,
                firstMember,
                secondMember,
                out TownPartyContinuationContext context) ||
            _cache.TryResolve(claimant, out _))
        {
            return default;
        }

        _profileByPlayer[info.Source] = claimant;
        TownPartyContinuationClaimResult claimResult = _continuationClaims.Submit(claimant, context);
        if (claimResult == TownPartyContinuationClaimResult.Rejected)
        {
            RPC_InvalidateContinuation(info.Source, originRaidCode, originLaunchRevision);
            return default;
        }

        if (claimResult == TownPartyContinuationClaimResult.ReadyToRestore)
        {
            if (!AreContinuationMembersAvailable(context) || !TrySpawnPreparation(context))
            {
                return default;
            }

            _continuationClaims.MarkRestored(context);
        }

        RPC_AcknowledgeContinuationClaim(
            info.Source,
            originRaidCode,
            originLaunchRevision,
            ContinuationClaimEpoch);
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestAbandonContinuation(
        NetworkString<_8> originRaidCode,
        int originLaunchRevision,
        NetworkString<_32> hostProfileId,
        int memberCount,
        NetworkString<_32> firstMember,
        NetworkString<_32> secondMember,
        RpcInfo info = default)
    {
        if (!HasStateAuthority || !TryResolveSender(info.Source, out ProfileId claimant) ||
            !TryDecodeContinuation(
                originRaidCode,
                originLaunchRevision,
                hostProfileId,
                memberCount,
                firstMember,
                secondMember,
                out TownPartyContinuationContext context) ||
            !_continuationClaims.Withdraw(claimant, context))
        {
            return default;
        }

        for (int index = 0; index < context.Members.Count; index++)
        {
            if (TryResolvePlayer(context.Members[index], out PlayerRef player))
            {
                RPC_InvalidateContinuation(player, originRaidCode, originLaunchRevision);
            }
        }

        if (_cache.TryResolve(claimant, out TownRaidPreparationNetworkController preparation) &&
            context.MatchesRoster(preparation.HostProfileId, preparation.Snapshot.Members))
        {
            if (claimant == preparation.HostProfileId)
            {
                AuthorityDissolvePreparation(preparation);
            }
            else
            {
                preparation.AuthorityTryRemoveMember(claimant);
            }
        }

        return default;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AcknowledgeContinuationClaim(
        [RpcTarget] PlayerRef target,
        NetworkString<_8> originRaidCode,
        int originLaunchRevision,
        int claimEpoch)
    {
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator == null ||
            !coordinator.TryGetPendingPartyContinuation(out TownPartyContinuationContext context) ||
            context.OriginRaidCode.Value != originRaidCode.ToString() ||
            context.OriginLaunchRevision != originLaunchRevision)
        {
            return;
        }

        _continuationClaimAcknowledged = true;
        _observedContinuationClaimEpoch = claimEpoch;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_InvalidateContinuation(
        [RpcTarget] PlayerRef target,
        NetworkString<_8> originRaidCode,
        int originLaunchRevision)
    {
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator == null ||
            !coordinator.TryGetPendingPartyContinuation(out TownPartyContinuationContext context) ||
            context.OriginRaidCode.Value != originRaidCode.ToString() ||
            context.OriginLaunchRevision != originLaunchRevision)
        {
            return;
        }

        coordinator.AbandonPartyContinuation(context);
        ResetLocalContinuationClaim();
    }

    private void ObserveLocalContinuation()
    {
        SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
        if (coordinator == null ||
            !coordinator.TryGetPendingPartyContinuation(out TownPartyContinuationContext context))
        {
            ResetLocalContinuationClaim();
            return;
        }

        if (_observedLocalContinuation == null || !_observedLocalContinuation.Equals(context))
        {
            _observedLocalContinuation = context;
            _continuationClaimAcknowledged = false;
            _continuationClaimRequested = true;
            _observedContinuationClaimEpoch = ContinuationClaimEpoch;
        }

        ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        if (TryGetPreparation(localProfile, out TownRaidPreparationNetworkController preparation) &&
            context.MatchesRoster(preparation.HostProfileId, preparation.Snapshot.Members))
        {
            coordinator.ConfirmPartyContinuationRestored(preparation.Snapshot);
            ResetLocalContinuationClaim();
            return;
        }

        if (_observedContinuationClaimEpoch != ContinuationClaimEpoch)
        {
            _observedContinuationClaimEpoch = ContinuationClaimEpoch;
            _continuationClaimAcknowledged = false;
            _continuationClaimRequested = true;
        }

        if (_continuationClaimAcknowledged || !_continuationClaimRequested ||
            Runner == null || Runner.GetPlayerObject(Runner.LocalPlayer) == null)
        {
            return;
        }

        _continuationClaimRequested = false;
        RPC_RequestRestoreContinuation(
            context.OriginRaidCode.Value,
            context.OriginLaunchRevision,
            context.HostProfileId.Value,
            context.Members.Count,
            context.Members[0].Value,
            context.Members.Count > 1 ? context.Members[1].Value : string.Empty);
    }

    private bool AreContinuationMembersAvailable(TownPartyContinuationContext context)
    {
        for (int index = 0; index < context.Members.Count; index++)
        {
            if (!TryResolvePlayer(context.Members[index], out _) || _cache.TryResolve(context.Members[index], out _))
            {
                return false;
            }
        }

        return true;
    }

    private bool TrySpawnPreparation(TownPartyContinuationContext context)
    {
        if (!_preparationPrefab.IsValid || !TryGenerateUniqueRaidCode(out RaidCode raidCode))
        {
            return false;
        }

        bool initialized = false;
        NetworkObject spawned = Runner.Spawn(
            _preparationPrefab,
            Vector3.zero,
            Quaternion.identity,
            null,
            (callbackRunner, networkObject) =>
            {
                if (networkObject.TryGetBehaviour(out TownRaidPreparationNetworkController preparation))
                {
                    initialized = preparation.TrySetSpawnInitialization(
                        callbackRunner,
                        networkObject,
                        Object.Id,
                        raidCode,
                        context.HostProfileId,
                        context.Members);
                }
            });

        if (spawned != null && initialized)
        {
            return true;
        }

        if (spawned != null && spawned.IsValid)
        {
            Runner.Despawn(spawned);
        }

        return false;
    }

    private static bool TryDecodeContinuation(
        NetworkString<_8> originRaidCode,
        int originLaunchRevision,
        NetworkString<_32> hostProfileId,
        int memberCount,
        NetworkString<_32> firstMember,
        NetworkString<_32> secondMember,
        out TownPartyContinuationContext context)
    {
        context = null;
        if (!RaidCode.TryParse(originRaidCode.ToString(), out RaidCode code) ||
            memberCount < 1 || memberCount > TownRaidPreparationRules.MaxMembers)
        {
            return false;
        }

        var members = new ProfileId[memberCount];
        members[0] = new ProfileId(firstMember.ToString());
        if (memberCount == 2)
        {
            members[1] = new ProfileId(secondMember.ToString());
        }

        return TownPartyContinuationContext.TryCreate(
            code,
            originLaunchRevision,
            new ProfileId(hostProfileId.ToString()),
            members,
            out context);
    }

    private void ResetLocalContinuationClaim()
    {
        _observedLocalContinuation = null;
        _continuationClaimAcknowledged = false;
        _continuationClaimRequested = false;
        _observedContinuationClaimEpoch = -1;
    }

    private void UpdateCache(TownRaidPreparationNetworkController preparation)
    {
        if (!_cache.RegisterOrUpdate(preparation, preparation.Snapshot))
        {
            _indexReady = !HasStateAuthority;
        }

        RefreshConflictState();
    }

    private void RefreshConflictState()
    {
        if (_cache.IsConsistent)
        {
            _conflictLogged = false;
            return;
        }

        if (!_conflictLogged)
        {
            _conflictLogged = true;
            Debug.LogError(
                $"{nameof(TownRaidPreparationDirectory)} detected duplicate RaidCode/ProfileId claims. " +
                "Conflicting mappings are unresolved and authoritative mutations are blocked until rebuild.",
                this);
        }
    }

    private bool TryGenerateUniqueRaidCode(out RaidCode code)
    {
        for (int attempt = 0; attempt < RandomCodeAttempts; attempt++)
        {
            string value = UnityEngine.Random.Range(0, 1_000_000).ToString("D6");
            if (RaidCode.TryParse(value, out code) && !_cache.TryResolve(code, out _))
            {
                return true;
            }
        }

        code = default;
        return false;
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

    private bool TrySend(in RpcInvokeInfo invokeInfo)
    {
        return Object != null && Object.IsValid && Runner != null &&
            (invokeInfo.SendMessageResult == RpcSendMessageResult.Sent ||
             invokeInfo.LocalInvokeResult == RpcLocalInvokeResult.Invoked);
    }

    private bool CanMutate => HasStateAuthority && _indexReady && _cache.IsConsistent;
    private bool CanSendRequest => Object != null && Object.IsValid && Runner != null;
    private bool CanSendOrdinaryPartyRequest =>
        CanSendRequest && !(SessionConnectionCoordinator.Instance?.HasPendingPartyContinuation ?? false);
}
