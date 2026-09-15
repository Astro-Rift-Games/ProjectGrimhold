using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>Authoritative Town boundary for concrete Raid preparations only.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class TownRaidPreparationDirectory : NetworkBehaviour, IPlayerLeft, IStateAuthorityChanged
{
    private const int AuthorityRebuildDelayTicks = 2;
    private const int RandomCodeAttempts = 128;

    [SerializeField] private NetworkPrefabRef _preparationPrefab;

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

    public bool RequestCreate() { if (!CanSendRequest) return false; RPC_RequestCreate(); return true; }
    public bool RequestJoin(string code) { if (!CanSendRequest || !RaidCode.TryParse(code, out _)) return false; RPC_RequestJoin(code); return true; }
    public bool RequestLeave() { if (!CanSendRequest) return false; RPC_RequestLeave(); return true; }
    public bool RequestSetReady(bool ready) { if (!CanSendRequest) return false; RPC_RequestSetReady(ready); return true; }
    public bool RequestStart() { if (!CanSendRequest) return false; RPC_RequestStart(); return true; }

    public override void Spawned()
    {
        Runner.GetComponent<TownRaidPreparationDirectoryContext>()?.Register(this);
        _indexReady = !HasStateAuthority;
        if (HasStateAuthority) BeginAuthorityRebuild();
    }

    public override void Render()
    {
        if (!_interactionRequested) return;
        _interactionRequested = false;
        PreparationInteractionRequested?.Invoke();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _rebuildTicksRemaining <= 0) return;
        _rebuildTicksRemaining--;
        if (_rebuildTicksRemaining == 0) RebuildAuthorityIndices();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        runner.GetComponent<TownRaidPreparationDirectoryContext>()?.Unregister(this);
        _interactionRequested = false;
        _indexReady = false;
        _preparations.Clear();
        _profileByPlayer.Clear();
    }

    public void StateAuthorityChanged()
    {
        if (HasStateAuthority) BeginAuthorityRebuild();
        else { _indexReady = true; _rebuildTicksRemaining = 0; }
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority || !_profileByPlayer.Remove(player, out ProfileId profile) || !_cache.TryResolve(profile, out TownRaidPreparationNetworkController preparation)) return;
        if (preparation.State == TownRaidPreparationState.Launching) { preparation.AuthorityHandlePlayerLeft(profile); return; }
        if (profile == preparation.HostProfileId) AuthorityDissolvePreparation(preparation);
        else preparation.AuthorityTryRemoveMember(profile);
    }

    public void NotifyLocalInteractionRequested() => _interactionRequested = true;
    public bool TryGetPreparation(ProfileId profile, out TownRaidPreparationNetworkController preparation)
    { preparation = null; return profile.IsValid && _cache.TryResolve(profile, out preparation); }
    public bool TryGetPreparation(RaidCode code, out TownRaidPreparationNetworkController preparation)
    { preparation = null; return code.IsValid && _cache.TryResolve(code, out preparation); }

    public bool TryResolvePlayer(ProfileId profile, out PlayerRef player)
    {
        TownPartyDirectory parties = Runner?.GetComponent<TownPartyDirectoryContext>()?.Directory;
        if (parties != null && parties.TryResolvePlayer(profile, out player)) return true;
        player = PlayerRef.None;
        foreach (KeyValuePair<PlayerRef, ProfileId> pair in _profileByPlayer) if (pair.Value == profile) { player = pair.Key; return true; }
        if (Runner == null) return false;
        foreach (PlayerRef candidate in Runner.ActivePlayers)
        {
            if (!TryResolveSender(candidate, out ProfileId resolved)) continue;
            _profileByPlayer[candidate] = resolved;
            if (resolved == profile) { player = candidate; return true; }
        }
        return false;
    }

    public bool TryGetLocalPartyContinuation(ProfileId localProfile, out TownPartyContinuationContext context)
    {
        context = null;
        TownPartyDirectory parties = Runner?.GetComponent<TownPartyDirectoryContext>()?.Directory;
        return parties != null && parties.TryGetParty(localProfile, out TownPartySnapshot party) &&
            TownPartyContinuationContext.TryCreate(party, out context);
    }

    public void RegisterPreparation(TownRaidPreparationNetworkController preparation)
    {
        if (preparation == null) return;
        if (!_preparations.Contains(preparation)) _preparations.Add(preparation);
        UpdateCache(preparation);
    }

    public void NotifyPreparationChanged(TownRaidPreparationNetworkController preparation)
    { if (preparation != null && _preparations.Contains(preparation)) UpdateCache(preparation); }

    public void UnregisterPreparation(TownRaidPreparationNetworkController preparation)
    {
        if (preparation == null) return;
        _preparations.Remove(preparation);
        _cache.Unregister(preparation);
        RefreshConflictState();
    }

    public bool AuthorityDissolvePreparation(TownRaidPreparationNetworkController preparation)
    {
        if (!CanMutate || preparation == null || preparation.Object == null || !preparation.Object.IsValid || !preparation.HasStateAuthority) return false;
        UnregisterPreparation(preparation);
        Runner.Despawn(preparation.Object);
        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestCreate(RpcInfo info = default)
    {
        if (!CanMutate || !_preparationPrefab.IsValid || !TryResolveSender(info.Source, out ProfileId creator) ||
            _cache.TryResolve(creator, out _) || !TryGenerateUniqueRaidCode(out RaidCode code)) return default;

        TownPartyDirectory partyDirectory = Runner.GetComponent<TownPartyDirectoryContext>()?.Directory;
        if (partyDirectory == null || !partyDirectory.TryGetParty(creator, out TownPartySnapshot party) ||
            !TownPartyRules.TryCreateRaidCreatorRoster(party, creator, out IReadOnlyList<ProfileId> roster)) return default;

        _profileByPlayer[info.Source] = creator;
        TrySpawnPreparation(code, creator, roster);
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestJoin(NetworkString<_8> requestedCode, RpcInfo info = default)
    {
        if (!CanMutate || !RaidCode.TryParse(requestedCode.ToString(), out RaidCode code) ||
            !TryResolveSender(info.Source, out ProfileId profile) || _cache.TryResolve(profile, out _) ||
            !_cache.TryResolve(code, out TownRaidPreparationNetworkController preparation) || !preparation.HasStateAuthority ||
            !preparation.AuthorityTryAddMember(profile)) return default;
        _profileByPlayer[info.Source] = profile;
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestLeave(RpcInfo info = default)
    {
        if (!CanMutate || !TryResolveSender(info.Source, out ProfileId profile) ||
            !_cache.TryResolve(profile, out TownRaidPreparationNetworkController preparation) || preparation.State != TownRaidPreparationState.Waiting) return default;
        _profileByPlayer[info.Source] = profile;
        if (profile == preparation.HostProfileId) AuthorityDissolvePreparation(preparation);
        else preparation.AuthorityTryRemoveMember(profile);
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestSetReady(NetworkBool ready, RpcInfo info = default)
    {
        if (!CanMutate || !TryResolveSender(info.Source, out ProfileId profile) ||
            !_cache.TryResolve(profile, out TownRaidPreparationNetworkController preparation) || !preparation.HasStateAuthority) return default;
        _profileByPlayer[info.Source] = profile;
        preparation.AuthorityTrySetReady(profile, ready);
        return default;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private RpcInvokeInfo RPC_RequestStart(RpcInfo info = default)
    {
        if (!CanMutate || !TryResolveSender(info.Source, out ProfileId profile) ||
            !_cache.TryResolve(profile, out TownRaidPreparationNetworkController preparation) || !preparation.HasStateAuthority) return default;
        _profileByPlayer[info.Source] = profile;
        preparation.AuthorityTryStart(profile);
        return default;
    }

    private bool TrySpawnPreparation(RaidCode code, ProfileId host, IReadOnlyList<ProfileId> members)
    {
        bool initialized = false;
        NetworkObject spawned = Runner.Spawn(_preparationPrefab, Vector3.zero, Quaternion.identity, null,
            (callbackRunner, networkObject) =>
            {
                if (networkObject.TryGetBehaviour(out TownRaidPreparationNetworkController controller))
                    initialized = controller.TrySetSpawnInitialization(callbackRunner, networkObject, Object.Id, code, host, members);
            });
        if (spawned != null && initialized) return true;
        if (spawned != null && spawned.IsValid) Runner.Despawn(spawned);
        Debug.LogError($"{nameof(TownRaidPreparationDirectory)} failed to spawn a preparation.", this);
        return false;
    }

    private void BeginAuthorityRebuild() { _indexReady = false; _rebuildTicksRemaining = AuthorityRebuildDelayTicks; }

    private void RebuildAuthorityIndices()
    {
        var entries = new List<KeyValuePair<TownRaidPreparationNetworkController, TownRaidPreparationSnapshot>>(_preparations.Count);
        for (int index = 0; index < _preparations.Count; index++)
        { TownRaidPreparationNetworkController preparation = _preparations[index]; if (preparation != null && preparation.Object != null && preparation.Object.IsValid) entries.Add(new(preparation, preparation.Snapshot)); }
        _profileByPlayer.Clear();
        if (Runner != null) foreach (PlayerRef player in Runner.ActivePlayers) if (TryResolveSender(player, out ProfileId profile)) _profileByPlayer[player] = profile;
        _indexReady = _cache.Rebuild(entries);
        RefreshConflictState();
    }

    private void UpdateCache(TownRaidPreparationNetworkController preparation)
    { if (!_cache.RegisterOrUpdate(preparation, preparation.Snapshot)) _indexReady = !HasStateAuthority; RefreshConflictState(); }

    private void RefreshConflictState()
    {
        if (_cache.IsConsistent) { _conflictLogged = false; return; }
        if (_conflictLogged) return;
        _conflictLogged = true;
        Debug.LogError($"{nameof(TownRaidPreparationDirectory)} detected duplicate RaidCode/ProfileId claims.", this);
    }

    private bool TryGenerateUniqueRaidCode(out RaidCode code)
    {
        for (int attempt = 0; attempt < RandomCodeAttempts; attempt++)
        { string value = UnityEngine.Random.Range(0, 1_000_000).ToString("D6"); if (RaidCode.TryParse(value, out code) && !_cache.TryResolve(code, out _)) return true; }
        code = default;
        return false;
    }

    private bool TryResolveSender(PlayerRef player, out ProfileId profile)
    {
        profile = default;
        NetworkObject playerObject = Runner != null && !player.IsNone ? Runner.GetPlayerObject(player) : null;
        if (playerObject == null || !playerObject.TryGetBehaviour(out SocialPlayerIdentity identity) || identity.Object.InputAuthority != player ||
            string.IsNullOrWhiteSpace(identity.ProfileId.ToString())) return false;
        profile = new ProfileId(identity.ProfileId.ToString());
        return true;
    }

    private bool CanSendRequest => Runner != null && Object != null && Object.IsValid && _indexReady && SessionConnectionCoordinator.Instance?.State == SessionConnectionState.Town;
    private bool CanMutate => HasStateAuthority && Runner != null && Object != null && Object.IsValid && _indexReady && _cache.IsConsistent;
}
