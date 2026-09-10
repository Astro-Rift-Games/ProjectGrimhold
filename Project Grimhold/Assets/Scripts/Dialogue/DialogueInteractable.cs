using Fusion;
using UnityEngine;

/// <summary>
/// World interaction endpoint for initiating dialogue sequences with NPCs or interactive objects.
/// Implements <see cref="IInteractable"/> for interaction discovery and validation,
/// and <see cref="IDialogueTrigger"/> to expose configured dialogue sequences.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class DialogueInteractable : NetworkBehaviour, IInteractable, IDialogueTrigger
{
    [Header("Dialogue Sequences")]
    [Tooltip("Primary sequence played on the first interaction.")]
    [SerializeField] private DialogueSequence _primarySequence;

    [Tooltip("Secondary/repeatable sequence played on subsequent interactions (optional).")]
    [SerializeField] private DialogueSequence _secondarySequence;

    private Collider2D[] _colliders;
    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;

    public DialogueSequence PrimarySequence => _primarySequence;
    public DialogueSequence SecondarySequence => _secondarySequence;

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
            Debug.LogError($"{nameof(DialogueInteractable)} requires an EntityRegistry on Runner.", this);
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
