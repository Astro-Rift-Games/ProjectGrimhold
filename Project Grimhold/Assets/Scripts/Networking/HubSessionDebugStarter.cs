using UnityEngine;

/// <summary>
/// TEMPORAL HOOK: Se debe retirar en el Paso 7 cuando se introduzca el SessionConnectionCoordinator.
/// Permite arrancar y apagar la sesión Shared manualmente desde el Inspector.
/// </summary>
[RequireComponent(typeof(HubSessionLauncher))]
public sealed class HubSessionDebugStarter : MonoBehaviour
{
    [SerializeField]
    private PlayerClassId _placeholderClass = PlayerClassId.Melee;

    private HubSessionLauncher _launcher;

    private void Awake()
    {
        _launcher = GetComponent<HubSessionLauncher>();
    }

    [ContextMenu("Start Hub Session")]
    public async void StartSession()
    {
        if (_launcher.Runner != null && _launcher.Runner.IsRunning)
        {
            Debug.LogWarning("Session is already running.");
            return;
        }

        Debug.Log("Attempting to start Hub Session...");
        bool success = await _launcher.StartHubSessionAsync(_placeholderClass);
        Debug.Log($"Hub Session Start Result: {success}");
    }

    [ContextMenu("Shutdown Hub Session")]
    public async void ShutdownSession()
    {
        if (_launcher.Runner == null)
        {
            Debug.LogWarning("No session to shutdown.");
            return;
        }

        Debug.Log("Attempting to shutdown Hub Session...");
        await _launcher.ShutdownAndDestroyRunnerAsync();
        Debug.Log("Hub Session Shutdown completed.");
    }
}
