using Fusion;
using UnityEngine;

/// <summary>
/// Town interaction adapter that opens local preparation presentation without owning preparation state.
/// Mutations remain explicit requests on <see cref="TownRaidPreparationDirectory"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class TownRaidNpcInteractable : NetworkBehaviour, IInteractable
{
    [SerializeField]
    private TownRaidPreparationDirectory _directory;

    private Collider2D[] _colliders;
    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public TownRaidPreparationDirectory PreparationDirectory => _directory;

    private void Awake()
    {
        _colliders = GetComponentsInChildren<Collider2D>(true);
    }

    public override void Spawned()
    {
        _registry = Runner.GetComponent<EntityRegistry>();
        _registeredId = Id;
        _isRegistered = _directory != null && _registry != null && _registry.TryRegisterEntity(_registeredId, this, _colliders);
        if (!_isRegistered)
        {
            Debug.LogError($"{nameof(TownRaidNpcInteractable)} requires a preparation directory and EntityRegistry.", this);
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
        if (!CanInteract(request))
        {
            return InteractionResult.Rejected(InteractionFailureReason.TargetUnavailable);
        }

        _directory.NotifyLocalInteractionRequested();
        return InteractionResult.Succeeded();
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
