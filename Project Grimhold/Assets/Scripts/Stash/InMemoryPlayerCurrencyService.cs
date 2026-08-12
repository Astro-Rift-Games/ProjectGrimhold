using System;
using UnityEngine;

/// <summary>
/// Compatibility adapter exposing the persistent store through the
/// currency service contract. It owns no currency state.
/// </summary>
public sealed class InMemoryPlayerCurrencyService : MonoBehaviour, IPlayerCurrencyService
{
    private LocalProfileStore _store;
    private long _lastKnownCurrency;

    public event Action<ProfileId> CurrencyChanged;

    public void Initialize(LocalProfileStore store)
    {
        if (_store != null) _store.ProfileCommitted -= OnProfileCommitted;
        _store = store;
        _lastKnownCurrency = store != null ? store.GetCurrency() : LocalProfileSnapshot.InitialCurrency;
        if (_store != null) _store.ProfileCommitted += OnProfileCommitted;
    }

    public long GetCurrency(ProfileId profileId) => IsProfile(profileId) ? _store.GetCurrency() : LocalProfileSnapshot.InitialCurrency;

    public StashOperationResult TryCreditCurrency(ProfileId profileId, long amount) =>
        IsProfile(profileId) ? _store.TryCreditCurrency(amount) : StashOperationResult.InvalidInventory;

    public StashOperationResult TryDebitCurrency(ProfileId profileId, long amount) =>
        IsProfile(profileId) ? _store.TryDebitCurrency(amount) : StashOperationResult.InvalidInventory;

    private bool IsProfile(ProfileId profileId) => _store != null && profileId == _store.ProfileId;

    private void OnProfileCommitted(ProfileId profileId)
    {
        long current = _store.GetCurrency();
        if (current == _lastKnownCurrency) return;
        _lastKnownCurrency = current;
        CurrencyChanged?.Invoke(profileId);
    }

    private void OnDestroy()
    {
        if (_store != null) _store.ProfileCommitted -= OnProfileCommitted;
    }
}
