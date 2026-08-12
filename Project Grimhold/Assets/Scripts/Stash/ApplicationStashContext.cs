using UnityEngine;

/// <summary>
/// Composition root for stash services at the application level.
/// Decouples consumers from concrete stash implementations.
/// </summary>
[DisallowMultipleComponent]
public sealed class ApplicationStashContext : MonoBehaviour
{
    public IPlayerStashService StashService { get; private set; }
    public IPlayerLoadoutService LoadoutService { get; private set; }
    public IPlayerCurrencyService CurrencyService { get; private set; }
    public IShopTransactionService ShopTransactionService { get; private set; }
    public LocalProfileStore Store { get; private set; }
    public LocalProfilePersistenceStatus PersistenceStatus => Store?.Status ?? LocalProfilePersistenceStatus.Unavailable;
    public string PersistenceError => Store?.LastError;

    public event System.Action<ProfileId> ProfileCommitted;

    /// <summary>
    /// Injects the concrete implementation of the stash service.
    /// This should only be called during initialization by a bootstrapper.
    /// </summary>
    public void Initialize(
        LocalProfileStore store,
        IPlayerStashService stashService,
        IPlayerLoadoutService loadoutService,
        IPlayerCurrencyService currencyService,
        IShopTransactionService shopTransactionService)
    {
        if (Store != null) Store.ProfileCommitted -= OnProfileCommitted;
        Store = store;
        StashService = stashService;
        LoadoutService = loadoutService;
        CurrencyService = currencyService;
        ShopTransactionService = shopTransactionService;
        if (Store != null) Store.ProfileCommitted += OnProfileCommitted;
    }

    private void OnProfileCommitted(ProfileId profileId) => ProfileCommitted?.Invoke(profileId);

    private void OnDestroy()
    {
        if (Store != null) Store.ProfileCommitted -= OnProfileCommitted;
    }
}
