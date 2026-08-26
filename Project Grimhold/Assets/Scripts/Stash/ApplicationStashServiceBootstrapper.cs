using UnityEngine;

/// <summary>
/// Creates one process-local stash context that survives scene and NetworkRunner transitions.
/// Its gameplay data is intentionally discarded when the application closes.
///
/// Initialization is split into two phases:
/// 1. BeforeSceneLoad creates the DontDestroyOnLoad GameObject.
/// 2. InitializeWithProfile is called by LoginFlowController after a successful login.
/// </summary>
public static class ApplicationStashServiceBootstrapper
{
    private const string ContextName = "ApplicationStashContext";

    private static ApplicationStashContext _context;
    private static LocalProfilePersistenceConfiguration _configuration;
    private static ProfileId _initializedProfileId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _context = null;
        _configuration = null;
        _initializedProfileId = default;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (Object.FindAnyObjectByType<ApplicationStashContext>() != null)
        {
            return;
        }

        _configuration = Resources.Load<LocalProfilePersistenceConfiguration>("LocalProfilePersistenceConfiguration");
        if (_configuration == null || _configuration.LootCatalog == null)
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Local profile configuration or loot catalog is missing.");
            return;
        }

        var contextObject = new GameObject(ContextName);
        _context = contextObject.AddComponent<ApplicationStashContext>();
        Object.DontDestroyOnLoad(contextObject);

        // Attempt immediate initialization if a valid ProfileId is already available
        // (e.g. during an Editor domain reload mid-session).
        var profileId = LocalProfileProvider.GetOrCreateLocalProfile();
        if (profileId.IsValid)
        {
            InitializeStore(profileId);
        }
        else
        {
            Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Context created. Waiting for login to provide a ProfileId.");
        }
    }

    /// <summary>
    /// Initializes the stash store for the given profile. Called by LoginFlowController
    /// after a successful login. Safe to call only once per profile per session.
    /// </summary>
    public static void InitializeWithProfile(ProfileId profileId)
    {
        if (!profileId.IsValid)
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] InitializeWithProfile called with an invalid ProfileId.");
            return;
        }

        if (_initializedProfileId == profileId)
        {
            Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Store already initialized for ProfileId {profileId.Value}. Skipping.");
            return;
        }

        if (_context == null)
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Context not created yet. Was BeforeSceneLoad suppressed?");
            return;
        }

        if (_configuration == null || _configuration.LootCatalog == null)
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Configuration unavailable during deferred initialization.");
            return;
        }

        InitializeStore(profileId);
    }

    private static void InitializeStore(ProfileId profileId)
    {
        var contextObject = _context.gameObject;

        var repository = new InMemoryLocalProfileRepository();
        if (!repository.Initialize(profileId, _configuration.LootCatalog))
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] In-memory profile unavailable: {repository.LastError}");
            return;
        }

        var store = new LocalProfileStore(
            repository,
            profileId,
            _configuration.LootCatalog,
            _configuration.RecoveryWeaponLootId);

        if (store.PendingReservation != null)
        {
            Debug.LogWarning($"[{nameof(ApplicationStashServiceBootstrapper)}] Rolling back orphaned loadout reservation from a previous session crash.");
            store.TryRollbackLoadoutReservation(store.PendingReservation.ReservationId);
        }

        var stashService = contextObject.AddComponent<InMemoryPlayerStashService>();
        var loadoutService = contextObject.AddComponent<InMemoryPlayerLoadoutService>();
        var currencyService = contextObject.AddComponent<InMemoryPlayerCurrencyService>();
        var shopTransactionService = contextObject.AddComponent<LocalShopTransactionService>();
        stashService.Initialize(store);
        loadoutService.Initialize(store);
        currencyService.Initialize(store);
        shopTransactionService.Initialize(store);
        _context.Initialize(store, stashService, loadoutService, currencyService, shopTransactionService);

        _initializedProfileId = profileId;
        Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Store initialized for ProfileId {profileId.Value}.");
    }
}
