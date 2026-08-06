using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LauncherShutdownListener : NetworkRunnerCallbacksAdapter
{
    private FusionSessionLauncher _launcher;
    private NetworkRunner _expectedRunner;

    public void Initialize(FusionSessionLauncher launcher, NetworkRunner runner)
    {
        _launcher = launcher;
        _expectedRunner = runner;
    }

    public override void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (runner != _expectedRunner) return;
        
        if (_launcher != null)
        {
            _launcher.ClearReferencesOnShutdown(runner);
        }
    }
}
