using UnityEngine;

/// <summary>
/// Ensures the existence of the persistent application stash context exactly once per application run.
/// </summary>
public static class ApplicationStashServiceBootstrapper
{
    private const string ContextName = "ApplicationStashContext";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        // Guard against duplicate initialization in case of domain reloads or manual invocation
        if (Object.FindAnyObjectByType<ApplicationStashContext>() != null)
        {
            return;
        }

        var contextObject = new GameObject(ContextName);
        var context = contextObject.AddComponent<ApplicationStashContext>();
        
        var configuration = Resources.Load<LocalProfilePersistenceConfiguration>("LocalProfilePersistenceConfiguration");
        if (configuration == null || configuration.LootCatalog == null)
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Local persistence configuration or loot catalog is missing.");
            Object.DontDestroyOnLoad(contextObject);
            return;
        }

        ProfileId profileId = LocalProfileProvider.GetOrCreateLocalProfile();
        var repository = new LocalProfileRepository(new LocalProfileFileStore(), Application.persistentDataPath);
        if (!repository.Initialize(profileId, configuration.LootCatalog))
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Persistence unavailable: {repository.LastError}");
            Object.DontDestroyOnLoad(contextObject);
            return;
        }

        var store = new LocalProfileStore(repository, profileId);
        var stashService = contextObject.AddComponent<InMemoryPlayerStashService>();
        var loadoutService = contextObject.AddComponent<InMemoryPlayerLoadoutService>();
        var currencyService = contextObject.AddComponent<InMemoryPlayerCurrencyService>();
        var shopTransactionService = contextObject.AddComponent<LocalShopTransactionService>();
        stashService.Initialize(store);
        loadoutService.Initialize(store);
        currencyService.Initialize(store);
        shopTransactionService.Initialize(store);
        context.Initialize(store, stashService, loadoutService, currencyService, shopTransactionService);

        Object.DontDestroyOnLoad(contextObject);

        Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Initialized {ContextName} successfully.");
    }
}
