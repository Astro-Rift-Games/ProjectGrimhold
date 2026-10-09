#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// Installs the synthetic <see cref="SandboxTestProfile"/> into the process-wide
/// <see cref="ApplicationStashContext"/> before a direct Host raid. It deliberately bypasses
/// <see cref="ApplicationStashServiceBootstrapper"/> (file-backed, backend-capable): only the in-memory
/// stash, loadout and currency services are attached, so there is no disk persistence, no remote
/// inventory service, no profile reconciliation and no shop service. It refuses to replace a profile
/// that is already active, so a logged-in or development profile is never overwritten or touched.
/// </summary>
public static class SandboxTestPlayerInstaller
{
    public static bool TryInstall(out string error)
    {
        ApplicationStashContext context = Object.FindAnyObjectByType<ApplicationStashContext>();
        if (context == null)
        {
            error = "ApplicationStashContext was not found.";
            return false;
        }

        if (context.Store != null)
        {
            error = SandboxTestProfile.IsSandboxProfile(context.ProfileId)
                ? null
                : $"A profile ('{context.ProfileId.Value}') is already active; the sandbox will not replace it.";
            return error == null;
        }

        var configuration = Resources.Load<LocalProfilePersistenceConfiguration>("LocalProfilePersistenceConfiguration");
        if (configuration == null || configuration.AbilityCatalog == null)
        {
            error = "LocalProfilePersistenceConfiguration or its ability catalog is missing.";
            return false;
        }

        if (!SandboxTestProfile.TryCreateStore(configuration.AbilityCatalog, configuration, out LocalProfileStore store, out error))
        {
            return false;
        }

        ExpeditionPreparationResult preparation = store.TryPrepareExpeditionEquipment();
        if (preparation != ExpeditionPreparationResult.Success)
        {
            error = $"The test player has no launchable loadout: {preparation}.";
            return false;
        }

        GameObject host = context.gameObject;
        var stashService = host.AddComponent<InMemoryPlayerStashService>();
        var loadoutService = host.AddComponent<InMemoryPlayerLoadoutService>();
        var currencyService = host.AddComponent<InMemoryPlayerCurrencyService>();
        stashService.Initialize(store);
        loadoutService.Initialize(store);
        currencyService.Initialize(store);
        context.Initialize(store, stashService, loadoutService, currencyService, null);

        LocalProfileProvider.SetRemoteCharacterId(SandboxTestProfile.Id);
        Debug.LogWarning(
            $"[{nameof(SandboxTestPlayerInstaller)}] SANDBOX TEST PLAYER active: {SandboxTestProfile.ProfileIdValue}. " +
            "In-memory only: no disk, no backend, no real profile.");
        error = null;
        return true;
    }
}
#endif
