using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LauncherShutdownListener : NetworkRunnerCallbacksAdapter
{
    private Action<NetworkRunner, ShutdownReason> _shutdownHandler;
    private NetworkRunner _expectedRunner;
    private TaskCompletionSource<bool> _initialSceneReady;

    public void Initialize(
        NetworkRunner runner,
        Action<NetworkRunner, ShutdownReason> shutdownHandler)
    {
        _expectedRunner = runner;
        _shutdownHandler = shutdownHandler;
        _initialSceneReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task<bool> WaitForInitialSceneAsync()
    {
        return _initialSceneReady?.Task ?? Task.FromResult(false);
    }

    public override void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner == _expectedRunner)
        {
            _initialSceneReady?.TrySetResult(true);
        }
    }

    public override void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (runner != _expectedRunner)
        {
            return;
        }

        _initialSceneReady?.TrySetResult(false);
        _shutdownHandler?.Invoke(runner, shutdownReason);
        _shutdownHandler = null;
        _expectedRunner = null;
    }

    /// <summary>
    /// Removes this lifecycle observer before an intentional shutdown. Unexpected shutdowns
    /// remain observable until the launcher explicitly begins disposal.
    /// </summary>
    public void Detach()
    {
        if (_expectedRunner != null)
        {
            _expectedRunner.RemoveCallbacks(this);
        }

        _initialSceneReady?.TrySetResult(false);
        _shutdownHandler = null;
        _expectedRunner = null;
    }
}
