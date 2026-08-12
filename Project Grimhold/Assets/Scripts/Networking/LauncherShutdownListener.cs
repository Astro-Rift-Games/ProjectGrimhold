using System;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LauncherShutdownListener : NetworkRunnerCallbacksAdapter
{
    private static readonly TimeSpan InitialSceneTimeout = TimeSpan.FromSeconds(30);

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

    /// <summary>
    /// Waits for the initial Fusion scene callback within a bounded startup lifetime.
    /// Cancellation belongs to the transition that owns the runner being created.
    /// </summary>
    public async Task<bool> WaitForInitialSceneAsync(CancellationToken cancellationToken = default)
    {
        if (_initialSceneReady == null)
        {
            return false;
        }

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task timeout = Task.Delay(InitialSceneTimeout, timeoutCancellation.Token);
        Task<bool> ready = _initialSceneReady.Task;
        Task completed = await Task.WhenAny(ready, timeout);
        if (completed != ready)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }

        timeoutCancellation.Cancel();
        return await ready;
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

    private void OnDestroy()
    {
        _initialSceneReady?.TrySetResult(false);
        _shutdownHandler = null;
        _expectedRunner = null;
    }
}
