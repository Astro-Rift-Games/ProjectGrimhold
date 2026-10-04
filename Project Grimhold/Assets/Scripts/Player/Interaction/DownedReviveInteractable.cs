using Fusion;
using UnityEngine;

/// <summary>
/// Makes a Downed player avatar a valid target of the held Interact that starts an assisted
/// recovery (DownedAndReviveArchitecture section 8). The avatar's primary interactable slot is
/// already owned by its corpse loot container, so this registers as a supplemental interactable
/// under the avatar's entity id; the avatar's existing interaction-layer trigger collider is
/// already mapped to that id by <see cref="CharacterBase"/>.
///
/// <see cref="CanInteract"/> is false for every non-Downed avatar, so it never shadows another
/// interactable in <see cref="InteractionResolver"/>. All gameplay rules live in
/// <see cref="PlayerDownedRecoveryNetworkController"/>; <see cref="Interact"/> only opens the session.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCharacter))]
[RequireComponent(typeof(PlayerDownedRecoveryNetworkController))]
public sealed class DownedReviveInteractable : NetworkBehaviour, IInteractable
{
    private PlayerCharacter _character;
    private PlayerDownedRecoveryNetworkController _recovery;
    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _registry = Runner.GetComponent<EntityRegistry>();
        _registeredId = Id;
        _isRegistered = _character != null && _recovery != null && _registry != null &&
                        _registry.TryRegisterSupplementalInteractable(_registeredId, this);
        if (!_isRegistered)
        {
            Debug.LogError(
                $"{nameof(DownedReviveInteractable)}: failed to register revive interactable '{name}' with ID {_registeredId}.",
                this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_isRegistered && _registry != null)
        {
            _registry.TryUnregisterSupplementalInteractable(_registeredId, this);
        }

        _isRegistered = false;
        _registry = null;
    }

    public bool CanInteract(in InteractionRequest request)
    {
        return TryResolveReviver(request, out PlayerCharacter reviver) &&
               _recovery.CanBeginAssisted(reviver);
    }

    public InteractionResult Interact(in InteractionRequest request)
    {
        return TryResolveReviver(request, out PlayerCharacter reviver) &&
               _recovery.TryBeginAssisted(reviver)
            ? InteractionResult.Succeeded()
            : InteractionResult.Rejected(InteractionFailureReason.TargetUnavailable);
    }

    private bool TryResolveReviver(in InteractionRequest request, out PlayerCharacter reviver)
    {
        reviver = null;
        return _isRegistered &&
               request.InteractorId.Value != 0 &&
               request.TargetId == Id &&
               request.InteractorId != Id &&
               _registry.TryGetCharacter(request.InteractorId, out ICharacter interactor) &&
               (reviver = interactor as PlayerCharacter) != null;
    }

    private void CacheDependencies()
    {
        _character ??= GetComponent<PlayerCharacter>();
        _recovery ??= GetComponent<PlayerDownedRecoveryNetworkController>();
    }
}
