using UnityEngine;

/// <summary>
/// Creates one process-local stash context that survives scene and NetworkRunner transitions.
/// Its gameplay data is intentionally discarded when the application closes.
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
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] Local profile configuration or loot catalog is missing.");
            Object.DontDestroyOnLoad(contextObject);
            return;
        }

        ProfileId profileId = LocalProfileProvider.GetOrCreateLocalProfile();
        var repository = new InMemoryLocalProfileRepository();
        if (!repository.Initialize(profileId, configuration.LootCatalog))
        {
            Debug.LogError($"[{nameof(ApplicationStashServiceBootstrapper)}] In-memory profile unavailable: {repository.LastError}");
            Object.DontDestroyOnLoad(contextObject);
            return;
        }

        var store = new LocalProfileStore(repository, profileId);
        var stashService = contextObject.AddComponent<InMemoryPlayerStashService>();
        var loadoutService = contextObject.AddComponent<InMemoryPlayerLoadoutService>();
        stashService.Initialize(store);
        loadoutService.Initialize(store);
        context.Initialize(store, stashService, loadoutService);

        Object.DontDestroyOnLoad(contextObject);

        Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Initialized process-local {ContextName} successfully.");
    }
}
