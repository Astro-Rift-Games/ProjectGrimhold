using UnityEngine;
using Grimhold.Backend;

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
    public static void InitializeWithProfile(ProfileId profileId, Grimhold.Backend.InventoryData? inventoryData = null)
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

        InitializeStore(profileId, inventoryData);
    }

    private static void InitializeStore(ProfileId profileId, Grimhold.Backend.InventoryData? inventoryData = null)
    {
        var contextObject = _context.gameObject;

        var repository = new InMemoryLocalProfileRepository();
        if (!repository.Initialize(profileId, _configuration.LootCatalog))
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] In-memory profile unavailable: {repository.LastError}");
            return;
        }

        if (inventoryData.HasValue)
        {
            HydrateSnapshot(repository.Snapshot, inventoryData.Value, _configuration.LootCatalog);
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
            
            // At this point we haven't created the RemoteInventoryService component yet.
            // But we can just use the client directly since we have the auth token in context
            var authToken = ApplicationAuthContext.Instance?.Token;
            if (!string.IsNullOrEmpty(authToken) && _configuration != null)
            {
                // In a real app we'd probably get the backend config properly, but let's just 
                // load it directly since it's a ScriptableObject
                var backendConfig = Resources.Load<BackendConfiguration>("BackendConfiguration");
                if (backendConfig != null)
                {
                    _ = InventoryClient.ClearPendingReservationAsync(backendConfig, authToken);
                }
            }
        }

        var stashService = contextObject.AddComponent<InMemoryPlayerStashService>();
        var loadoutService = contextObject.AddComponent<InMemoryPlayerLoadoutService>();
        var currencyService = contextObject.AddComponent<InMemoryPlayerCurrencyService>();
        var shopTransactionService = contextObject.AddComponent<LocalShopTransactionService>();
        
        // Add RemoteInventoryService to handle backend operations
        var remoteInventoryService = contextObject.AddComponent<RemoteInventoryService>();
        remoteInventoryService.Initialize(_configuration, store);

        stashService.Initialize(store);
        loadoutService.Initialize(store);
        currencyService.Initialize(store);
        shopTransactionService.Initialize(store);
        _context.Initialize(store, stashService, loadoutService, currencyService, shopTransactionService);

        _initializedProfileId = profileId;
        Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Store initialized for ProfileId {profileId.Value}.");
    }

    private static void HydrateSnapshot(LocalProfileSnapshot snapshot, Grimhold.Backend.InventoryData data, LootDefinitionCatalog catalog)
    {
        if (data.stash != null)
        {
            foreach (var item in data.stash)
            {
                if (catalog.TryGet(item.lootId, out _))
                {
                    snapshot.Stash.Add(new StashItem(new LootId(item.lootId), item.amount));
                }
            }
        }

        if (data.loadout != null)
        {
            foreach (var item in data.loadout)
            {
                if (catalog.TryGet(item.lootId, out _))
                {
                    snapshot.Loadout.Add(new StashItem(new LootId(item.lootId), item.amount));
                }
            }
        }

        var eq = data.preparedEquipment;
        snapshot.PreparedEquipment = new PreparedEquipmentLoadout(
            string.IsNullOrEmpty(eq.weaponSlot1) ? default : new LootId(eq.weaponSlot1),
            string.IsNullOrEmpty(eq.weaponSlot2) ? default : new LootId(eq.weaponSlot2),
            string.IsNullOrEmpty(eq.helmet) ? default : new LootId(eq.helmet),
            string.IsNullOrEmpty(eq.armor) ? default : new LootId(eq.armor),
            string.IsNullOrEmpty(eq.gloves) ? default : new LootId(eq.gloves),
            string.IsNullOrEmpty(eq.boots) ? default : new LootId(eq.boots)
        );

        if (data.pendingReservation.reservationId != null)
        {
            var res = data.pendingReservation;
            var resItems = new System.Collections.Generic.List<StashItem>();
            if (res.items != null)
            {
                foreach (var item in res.items)
                {
                    if (catalog.TryGet(item.lootId, out _))
                    {
                        resItems.Add(new StashItem(new LootId(item.lootId), item.amount));
                    }
                }
            }
            var resEq = res.preparedEquipment;
            var preparedResEq = new PreparedEquipmentLoadout(
                string.IsNullOrEmpty(resEq.weaponSlot1) ? default : new LootId(resEq.weaponSlot1),
                string.IsNullOrEmpty(resEq.weaponSlot2) ? default : new LootId(resEq.weaponSlot2),
                string.IsNullOrEmpty(resEq.helmet) ? default : new LootId(resEq.helmet),
                string.IsNullOrEmpty(resEq.armor) ? default : new LootId(resEq.armor),
                string.IsNullOrEmpty(resEq.gloves) ? default : new LootId(resEq.gloves),
                string.IsNullOrEmpty(resEq.boots) ? default : new LootId(resEq.boots)
            );

            snapshot.PendingReservation = new PendingLoadoutReservation(res.reservationId, resItems, preparedResEq);
        }
    }

    /// <summary>
    /// Resets the application stash context so that a new authenticated profile can be loaded.
    /// This destroys the in-memory services but preserves the DDOL GameObject.
    /// </summary>
    public static void ResetForLogout()
    {
        if (_context != null)
        {
            var contextObject = _context.gameObject;
            
            // Destroy all dynamically added service components
            foreach (var comp in contextObject.GetComponents<MonoBehaviour>())
            {
                // We don't want to destroy the context itself, just the services
                if (comp != _context)
                {
                    Object.Destroy(comp);
                }
            }

            _context.Initialize(null, null, null, null, null);
        }

        _initializedProfileId = default;
        Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Context reset for logout.");
    }
}
