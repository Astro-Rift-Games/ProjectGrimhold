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
    public BuybackLedger BuybackLedger { get; } = new();
    public IBuybackService BuybackService => ShopTransactionService as IBuybackService;
    public IBuybackEligibilityPolicy BuybackEligibility { get; } = new EquipmentBuybackEligibilityPolicy();

    /// <summary>Hydrates session-only state separately from the durable local snapshot.</summary>
    public void HydrateBuyback(Grimhold.Backend.InventoryData inventory) => BuybackLedger.Replace(inventory.buyback);
    public ConfirmedProgressionFeedbackQueue ProgressionFeedback { get; } = new();
    public ProfileId ProfileId => Store != null ? Store.ProfileId : default;
    public bool IsAvailable => Store != null && Store.IsAvailable;
    public LocalProfilePersistenceStatus PersistenceStatus => Store?.Status ?? LocalProfilePersistenceStatus.Unavailable;
    public string PersistenceError => Store?.LastError;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public RuntimeAttributeOverrideSession RuntimeAttributeOverrides { get; } = new();
#endif

    public event System.Action<ProfileId> ProfileCommitted;

    private StashTransferCoordinator _coordinator;
    private ProfileId _coordinatorProfile;
    private IPlayerStashService _coordinatorStash;
    private IPlayerLoadoutService _coordinatorLoadout;
    private IRemoteInventoryService _coordinatorRemote;
    private IProfileReconciliation _coordinatorReconciliation;

    /// <summary>
    /// One application queue for the bound profile/services, retained across screen lifecycles.
    /// Catalog and Equipment capabilities are supplied per operation, not retained from a UI.
    /// </summary>
    public StashTransferCoordinator GetTransferCoordinator()
    {
        if (!IsAvailable || !ProfileId.IsValid || StashService == null || LoadoutService == null) return null;
        var remote = GetComponent<IRemoteInventoryService>();
        var reconciliation = GetComponent<IProfileReconciliation>();
        if (_coordinator != null && _coordinatorProfile == ProfileId &&
            ReferenceEquals(_coordinatorStash, StashService) && ReferenceEquals(_coordinatorLoadout, LoadoutService) &&
            ReferenceEquals(_coordinatorRemote, remote) && ReferenceEquals(_coordinatorReconciliation, reconciliation))
            return _coordinator;

        _coordinator = new StashTransferCoordinator(ProfileId, StashService, LoadoutService, remote, reconciliation);
        _coordinatorProfile = ProfileId;
        _coordinatorStash = StashService;
        _coordinatorLoadout = LoadoutService;
        _coordinatorRemote = remote;
        _coordinatorReconciliation = reconciliation;
        return _coordinator;
    }

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
        if (Store != null)
        {
            Store.ProfileCommitted -= OnProfileCommitted;
            Store.ProgressionCommitted -= OnProgressionCommitted;
        }
        ProgressionFeedback.Clear();
        BuybackLedger.Replace(default);
        Store = store;
        StashService = stashService;
        LoadoutService = loadoutService;
        CurrencyService = currencyService;
        ShopTransactionService = shopTransactionService;
        if (Store != null)
        {
            Store.ProfileCommitted += OnProfileCommitted;
            Store.ProgressionCommitted += OnProgressionCommitted;
        }
    }

    private void OnProgressionCommitted(ProfileId profileId, ExperienceApplicationResult result) =>
        ProgressionFeedback.Record(profileId, result);

    private void OnProfileCommitted(ProfileId profileId) => ProfileCommitted?.Invoke(profileId);

    private void OnDestroy()
    {
        if (Store != null)
        {
            Store.ProfileCommitted -= OnProfileCommitted;
            Store.ProgressionCommitted -= OnProgressionCommitted;
        }
    }
}
