using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(SocialPlayerIdentity))]
public sealed class SocialPlayerInteractable : NetworkBehaviour, IInteractable
{
    [SerializeField]
    private SocialPlayerIdentity _identity;

    [SerializeField]
    private Collider2D _interactionCollider;

    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public ProfileId ProfileId => _identity != null
        ? new ProfileId(_identity.ProfileId.ToString())
        : default;

    public string PromptText
    {
        get
        {
            string displayName = _identity != null ? _identity.DisplayName.ToString() : string.Empty;
            return string.IsNullOrWhiteSpace(displayName) ? null : $"Invitar a Grupo a {displayName}";
        }
    }

    private void Awake()
    {
        if (_identity == null)
        {
            _identity = GetComponent<SocialPlayerIdentity>();
        }
    }

    public override void Spawned()
    {
        _registry = Runner.GetComponent<EntityRegistry>();
        _registeredId = Id;
        Collider2D[] colliders = _interactionCollider != null ? new[] { _interactionCollider } : null;
        _isRegistered = _identity != null && ProfileId.IsValid && _registry != null &&
            _interactionCollider != null && _interactionCollider.isTrigger &&
            _registry.TryRegisterEntity(_registeredId, this, colliders);
        if (!_isRegistered)
        {
            Debug.LogError($"{nameof(SocialPlayerInteractable)} requires valid identity and a dedicated trigger collider.", this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => Unregister();

    public bool CanInteract(in InteractionRequest request)
    {
        return _isRegistered && Id.Value != 0 && ProfileId.IsValid && request.InteractorId.Value != 0 &&
            request.InteractorId != Id && request.TargetId == Id;
    }

    public InteractionResult Interact(in InteractionRequest request)
    {
        return CanInteract(request)
            ? InteractionResult.Succeeded()
            : InteractionResult.Rejected(InteractionFailureReason.TargetUnavailable);
    }

    private void OnDestroy() => Unregister();

    private void Unregister()
    {
        if (_isRegistered && _registry != null)
        {
            _registry.TryUnregisterEntity(_registeredId, this);
        }

        _isRegistered = false;
        _registry = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_identity == null)
        {
            _identity = GetComponent<SocialPlayerIdentity>();
        }
    }
#endif
}
