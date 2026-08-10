using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Compatibility adapter exposing the persistent store through the existing
/// stash service contract. It owns no stash state.
/// </summary>
public sealed class InMemoryPlayerStashService : MonoBehaviour, IPlayerStashService
{
    private LocalProfileStore _store;

    public event Action<ProfileId> StashChanged;

    public void Initialize(LocalProfileStore store)
    {
        if (_store != null) _store.ProfileCommitted -= OnProfileCommitted;
        _store = store;
        if (_store != null) _store.ProfileCommitted += OnProfileCommitted;
    }

    public IReadOnlyList<StashItem> GetStash(ProfileId profileId) => IsProfile(profileId) ? _store.GetStash() : Array.Empty<StashItem>();

    public StashOperationResult TryConsumeLoot(ProfileId profileId, LootId lootId, int amount) =>
        IsProfile(profileId) ? _store.TryConsumeLoot(lootId, amount) : StashOperationResult.InvalidInventory;

    public StashOperationResult TrySecureLoot(ProfileId profileId, IReadOnlyList<StashItem> items) =>
        IsProfile(profileId) ? _store.TrySecureLoot(items) : StashOperationResult.InvalidInventory;

    private bool IsProfile(ProfileId profileId) => _store != null && profileId == _store.ProfileId;

    private void OnProfileCommitted(ProfileId profileId) => StashChanged?.Invoke(profileId);

    private void OnDestroy()
    {
        if (_store != null) _store.ProfileCommitted -= OnProfileCommitted;
    }
}
