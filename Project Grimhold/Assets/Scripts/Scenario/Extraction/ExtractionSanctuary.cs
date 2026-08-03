using Fusion;
using UnityEngine;

/// <summary>
/// Network sanctuary whose replicated owner is the sole authoritative reservation state.
/// Assignment discovery and selection are delegated to the runner-local assignment service.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class ExtractionSanctuary : NetworkBehaviour, IExtractionSanctuary
{
    private EntityRegistry _registry;
    private ExtractionSanctuaryAssignmentService _assignmentService;
    private EntityId _registeredId;
    private bool _isRegistryRegistered;
    private bool _isServiceRegistered;
    private bool _compositionValid;

    [Networked]
    private int OwnerIdValue { get; set; }

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public EntityId OwnerId => new EntityId(OwnerIdValue);
    public bool IsReserved => OwnerIdValue != 0;

    public override void Spawned()
    {
        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        _assignmentService = Runner != null
            ? Runner.GetComponent<ExtractionSanctuaryAssignmentService>()
            : null;
        _registeredId = Id;

        if (HasStateAuthority)
        {
            OwnerIdValue = 0;
        }

        if (_registry == null || _assignmentService == null || _registeredId.Value == 0)
        {
            Debug.LogError(
                $"{nameof(ExtractionSanctuary)} requires a valid identity, runner registry, and assignment service.",
                this);
            return;
        }

        _isRegistryRegistered = _registry.TryRegisterExtractionSanctuary(_registeredId, this);
        if (!_isRegistryRegistered)
        {
            Debug.LogError(
                $"{nameof(ExtractionSanctuary)} could not register sanctuary {_registeredId.Value} in the runner registry.",
                this);
            return;
        }

        _isServiceRegistered = _assignmentService.TryRegisterSanctuary(_registeredId, this);
        if (!_isServiceRegistered)
        {
            _registry.TryUnregisterExtractionSanctuary(_registeredId, this);
            _isRegistryRegistered = false;
            Debug.LogError(
                $"{nameof(ExtractionSanctuary)} could not register sanctuary {_registeredId.Value} in the assignment service.",
                this);
            return;
        }

        _compositionValid = true;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Unregister();
    }

    public bool IsOwnedBy(EntityId playerId)
    {
        return playerId.Value != 0 && OwnerIdValue == playerId.Value;
    }

    /// <summary>
    /// Reserves this sanctuary under State Authority. Repeating the same owner is idempotent;
    /// a different owner can never replace the reservation during the expedition.
    /// </summary>
    public bool TryReserve(EntityId playerId)
    {
        if (!_compositionValid || playerId.Value == 0 || !HasStateAuthority)
        {
            return false;
        }

        if (OwnerIdValue == playerId.Value)
        {
            return true;
        }

        if (OwnerIdValue != 0)
        {
            return false;
        }

        OwnerIdValue = playerId.Value;
        return true;
    }

    private void Unregister()
    {
        if (_isServiceRegistered && _assignmentService != null)
        {
            _assignmentService.TryUnregisterSanctuary(_registeredId, this);
        }

        if (_isRegistryRegistered && _registry != null)
        {
            _registry.TryUnregisterExtractionSanctuary(_registeredId, this);
        }

        _isServiceRegistered = false;
        _isRegistryRegistered = false;
        _compositionValid = false;
        _registeredId = default;
    }

    private void OnDestroy()
    {
        Unregister();
    }
}
