using Fusion;
using UnityEngine;

/// <summary>
/// Social Town interaction endpoint for a Merchant.
/// It only validates and confirms an interaction; it delegates economic transaction 
/// routing to the UI and the MerchantNetworkController.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class TownMerchantNpcInteractable : NetworkBehaviour, IInteractable
{
    private Collider2D[] _colliders;
    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    private void Awake()
    {
        _colliders = GetComponentsInChildren<Collider2D>(true);
    }

    public override void Spawned()
    {
        _registry = Runner.GetComponent<EntityRegistry>();
        _registeredId = Id;
        _isRegistered = _registry != null && _registry.TryRegisterEntity(_registeredId, this, _colliders);
        if (!_isRegistered)
        {
            Debug.LogError($"{nameof(TownMerchantNpcInteractable)} requires an EntityRegistry.", this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Unregister();
    }

    public bool CanInteract(in InteractionRequest request)
    {
        return _isRegistered && request.InteractorId.Value != 0 && request.TargetId == Id;
    }

    public InteractionResult Interact(in InteractionRequest request)
    {
        return CanInteract(request)
            ? InteractionResult.Succeeded()
            : InteractionResult.Rejected(InteractionFailureReason.TargetUnavailable);
    }

    private void OnDestroy()
    {
        Unregister();
    }

    private void Unregister()
    {
        if (_isRegistered && _registry != null)
        {
            _registry.TryUnregisterEntity(_registeredId, this);
        }

        _isRegistered = false;
        _registry = null;
    }
}
