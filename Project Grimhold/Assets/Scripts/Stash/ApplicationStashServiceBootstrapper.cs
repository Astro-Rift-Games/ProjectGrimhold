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
        
        var inMemoryService = contextObject.AddComponent<InMemoryPlayerStashService>();
        var loadoutService = contextObject.AddComponent<InMemoryPlayerLoadoutService>();
        context.Initialize(inMemoryService, loadoutService);

        Object.DontDestroyOnLoad(contextObject);

        Debug.Log($"[{nameof(ApplicationStashServiceBootstrapper)}] Initialized {ContextName} successfully.");
    }
}
