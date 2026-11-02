using Fusion;
using UnityEngine;

/// <summary>
/// Runner-local interactable whose lifecycle is written only by State Authority.
/// The same blocker supplies movement, projectile and vision occlusion.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkDoor : NetworkBehaviour, IInteractable
{
    private const int WorldCollisionLayer = 6;
    private const int InteractableLayer = 8;
    private const int CharacterAndEnemyMask = (1 << 7) | (1 << 10);

    [SerializeField]
    private Collider2D _blocker;

    [SerializeField]
    private Collider2D _interactionCollider;

    [SerializeField]
    private LayerMask _occupantMask = CharacterAndEnemyMask;

    [SerializeField, Min(0.01f)]
    private float _transitionDuration = 0.4f;

    [Networked] public DungeonDoorState State { get; private set; }
    [Networked] public DungeonDoorOpenDirection OpenDirection { get; private set; }
    [Networked] public TickTimer TransitionTimer { get; private set; }

    // A single hit is sufficient: every matching non-trigger body blocks closing.
    private readonly Collider2D[] _occupants = new Collider2D[1];
    private ContactFilter2D _occupantFilter;
    private EntityRegistry _registry;
    private EntityId _registeredId;
    private PathfindingGrid _grid;
    private bool _isRegistered;
    private bool _hasAppliedBlocker;
    private Bounds _blockerBounds;

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public string PromptText => DungeonDoorStateRules.ResolvePrompt(State);
    public bool IsReady => _isRegistered && Object != null && Object.IsValid;

    /// <summary>Presentation reads the replicated deadline; it never advances the lifecycle.</summary>
    public float TransitionProgress => DungeonDoorStateRules.IsStable(State)
        ? 1f
        : Mathf.Clamp01(1f - (TransitionTimer.RemainingTime(Runner) ?? 0f) / _transitionDuration);

    public override void Spawned()
    {
        _registry = Runner.GetComponent<EntityRegistry>();
        _isRegistered = false;
        _hasAppliedBlocker = false;
        if (!ValidateDependencies())
        {
            LogConfigurationError();
            return;
        }

        _registeredId = Id;
        _isRegistered = _registry.TryRegisterEntity(_registeredId, this, new[] { _interactionCollider });
        if (!_isRegistered)
        {
            LogConfigurationError();
            return;
        }

        _grid = FindFirstObjectByType<PathfindingGrid>();
        _occupantFilter = new ContactFilter2D { useTriggers = false };
        _occupantFilter.SetLayerMask(_occupantMask);

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            State = DungeonDoorState.Closed;
            OpenDirection = DungeonDoorOpenDirection.North;
            TransitionTimer = TickTimer.None;
        }

        // Disabled colliders report empty bounds. Cache the fixed doorway footprint before
        // applying even a restored Open state, and retain it while the blocker is disabled.
        // A freshly spawned door has not reached the physics world yet; sync once so its
        // bounds and the first navigation refresh use the spawned pose.
        Physics2D.SyncTransforms();
        _blocker.enabled = true;
        _blockerBounds = _blocker.bounds;
        ApplyBlockerState();
    }

    public bool CanInteract(in InteractionRequest request)
    {
        return IsReady && request.TargetId == Id && DungeonDoorStateRules.IsStable(State);
    }

    public InteractionResult Interact(in InteractionRequest request)
    {
        if (!IsReady || !HasStateAuthority || !CanInteract(request))
        {
            return InteractionResult.Rejected(InteractionFailureReason.TargetUnavailable);
        }

        bool occupied = State == DungeonDoorState.Open && Physics2D.OverlapBox(
            _blockerBounds.center, _blockerBounds.size, 0f, _occupantFilter, _occupants) > 0;
        if (!DungeonDoorStateRules.TryBeginToggle(State, occupied, out DungeonDoorState next))
        {
            return InteractionResult.Rejected(InteractionFailureReason.TargetUnavailable);
        }

        if (State == DungeonDoorState.Closed)
        {
            if (!_registry.TryGetTransform(request.InteractorId, out Transform interactor))
            {
                return InteractionResult.Rejected(InteractionFailureReason.InteractorUnavailable);
            }

            OpenDirection = DungeonDoorStateRules.ResolveOpenDirection(interactor.position.y, transform.position.y);
        }

        State = next;
        TransitionTimer = TickTimer.CreateFromSeconds(Runner, _transitionDuration);
        ApplyBlockerState();
        return InteractionResult.Succeeded();
    }

    public override void FixedUpdateNetwork()
    {
        if (!IsReady || !HasStateAuthority || DungeonDoorStateRules.IsStable(State) ||
            !TransitionTimer.Expired(Runner))
        {
            return;
        }

        State = DungeonDoorStateRules.CompleteTransition(State);
        TransitionTimer = TickTimer.None;
        ApplyBlockerState();
    }

    public override void Render()
    {
        if (IsReady)
        {
            ApplyBlockerState();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => Unregister();

    private void OnDestroy() => Unregister();

    private void ApplyBlockerState()
    {
        bool blocksPassage = DungeonDoorStateRules.BlocksPassage(State);
        if (_hasAppliedBlocker && _blocker.enabled == blocksPassage)
        {
            return;
        }

        if (_blocker.enabled)
        {
            _blockerBounds = _blocker.bounds;
        }

        _blocker.enabled = blocksPassage;
        if (blocksPassage)
        {
            _blockerBounds = _blocker.bounds;
        }

        _hasAppliedBlocker = true;
        if (HasStateAuthority && _grid != null && _grid.IsBuilt)
        {
            _grid.RefreshRegion(_blockerBounds);
        }
    }

    private bool ValidateDependencies()
    {
        return _registry != null && _blocker != null && !_blocker.isTrigger &&
            _blocker.gameObject.activeInHierarchy && _blocker.gameObject.layer == WorldCollisionLayer &&
            _blocker.GetComponentInParent<NetworkObject>() == Object &&
            _interactionCollider != null && _interactionCollider != _blocker &&
            _interactionCollider.isTrigger && _interactionCollider.enabled &&
            _interactionCollider.gameObject.activeInHierarchy &&
            _interactionCollider.gameObject.layer == InteractableLayer &&
            _interactionCollider.GetComponentInParent<NetworkObject>() == Object &&
            (_occupantMask.value & CharacterAndEnemyMask) == CharacterAndEnemyMask &&
            _transitionDuration > 0f && !float.IsNaN(_transitionDuration) && !float.IsInfinity(_transitionDuration);
    }

    private void LogConfigurationError()
    {
        Debug.LogError($"{nameof(NetworkDoor)} requires a runner EntityRegistry with a unique door registration, " +
            "a non-trigger WorldCollision blocker and a dedicated Interactable trigger under its NetworkObject, " +
            "a Character + Enemies occupant mask and a finite positive transition duration.", this);
    }

    private void Unregister()
    {
        if (_isRegistered && _registry != null)
        {
            _registry.TryUnregisterEntity(_registeredId, this);
        }

        _isRegistered = false;
        _registry = null;
        _grid = null;
    }
}
