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
public sealed class TownRaidPreparationDirectory : NetworkBehaviour, IPlayerLeft, IStateAuthorityChanged
{
    private const int AuthorityRebuildDelayTicks = 2;
    private const int RandomCodeAttempts = 128;

    [SerializeField]
    private NetworkPrefabRef _preparationPrefab;

    private readonly List<TownRaidPreparationNetworkController> _preparations = new();
    private readonly TownRaidPreparationDirectoryCache<TownRaidPreparationNetworkController> _cache = new();
    private readonly Dictionary<PlayerRef, ProfileId> _profileByPlayer = new();
    private bool _interactionRequested;
    private bool _indexReady;
    private bool _conflictLogged;
    private int _rebuildTicksRemaining;

    public event Action PreparationInteractionRequested;

    public bool IsIndexReady => _indexReady;
    public int PreparationCount => _preparations.Count;

    public bool RequestCreate() => CanSendRequest && TrySend(RPC_RequestCreate());
    public bool RequestJoin(string code) =>
        CanSendRequest && RaidCode.TryParse(code, out _) && TrySend(RPC_RequestJoin(code));
    public bool RequestLeave() => CanSendRequest && TrySend(RPC_RequestLeave());
    public bool RequestSetReady(bool isReady) => CanSendRequest && TrySend(RPC_RequestSetReady(isReady));
    public bool RequestStart() => CanSendRequest && TrySend(RPC_RequestStart());

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
        RefreshConflictState();
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
}
