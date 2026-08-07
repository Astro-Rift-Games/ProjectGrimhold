using Fusion;
using UnityEngine;

/// <summary>
/// Adapts the Fusion player network lifecycle to update the local camera tracking target.
/// This component is the presentation adapter for local player tracking.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalPlayerCameraBinder : NetworkBehaviour
{
    /// <summary>
    /// Singleton reference to the current active local player binder.
    /// </summary>
    public static LocalPlayerCameraBinder LocalPlayerInstance { get; private set; }

    private bool _registeredAsLocalTarget;

    public override void Spawned()
    {
        TryBindAsLocalPlayer();
    }

    public void TryBindAsLocalPlayer()
    {
        if (!HasInputAuthority || _registeredAsLocalTarget)
        {
            return;
        }

        _registeredAsLocalTarget = true;
        LocalPlayerInstance = this;

        if (LocalCameraController.Instance != null)
        {
            LocalCameraController.Instance.SetTarget(transform);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterLocalTarget();
    }

    private void OnDestroy()
    {
        UnregisterLocalTarget();
    }

    private void UnregisterLocalTarget()
    {
        if (!_registeredAsLocalTarget)
        {
            return;
        }

        if (LocalCameraController.Instance != null)
        {
            LocalCameraController.Instance.ClearTarget(transform);
        }

        if (LocalPlayerInstance == this)
        {
            LocalPlayerInstance = null;
        }

        _registeredAsLocalTarget = false;
    }
}
