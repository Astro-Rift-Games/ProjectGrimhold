using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative reservation and ritual endpoint for one individual extraction point.
/// The co-located ExtractionZone shares this NetworkObject identity while retaining its
/// independent registry capability and geometry responsibility.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(ExtractionZone))]
[RequireComponent(typeof(Collider2D))]
[DefaultExecutionOrder(90)]
public sealed class ExtractionSanctuary : NetworkBehaviour, IExtractionSanctuary, IInteractable
{
    [SerializeField]
    private ExtractionConfig _config;

    [SerializeField]
    private ExtractionZone _extractionZone;

    [SerializeField]
    private Collider2D _interactionCollider;

    private EntityRegistry _registry;
    private ExtractionSanctuaryAssignmentService _assignmentService;
    private Collider2D[] _registeredColliders;
    private EntityId _registeredId;
    private bool _isRegistryRegistered;
    private bool _isServiceRegistered;
    private bool _isEntityRegistered;
    private bool _compositionValid;

    [Networked]
    private int OwnerIdValue { get; set; }

    [Networked]
    public ExtractionRitualState RitualState { get; private set; }

    [Networked]
    private TickTimer RitualTimer { get; set; }

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public EntityId OwnerId => new EntityId(OwnerIdValue);
    public bool IsReserved => OwnerIdValue != 0;

    private void Awake()
    {
        CacheComposition();
    }

    public override void Spawned()
    {
        CacheComposition();
        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        _assignmentService = Runner != null
            ? Runner.GetComponent<ExtractionSanctuaryAssignmentService>()
            : null;
        _registeredId = Id;

        if (HasStateAuthority)
        {
            OwnerIdValue = 0;
            RitualState = ExtractionRitualState.NotStarted;
            RitualTimer = TickTimer.None;
            _extractionZone?.TrySetAvailability(false);
        }

        if (!ValidateComposition())
        {
            Debug.LogError(
                $"{nameof(ExtractionSanctuary)} requires a valid shared identity, configuration, zone, collider, registry, and assignment service.",
                this);
            return;
        }

        _isRegistryRegistered = _registry.TryRegisterExtractionSanctuary(_registeredId, this);
        if (!_isRegistryRegistered)
        {
            Debug.LogError(
                $"{nameof(ExtractionSanctuary)} could not register Sanctuary {_registeredId.Value} in the runner registry.",
                this);
            return;
        }

        _isServiceRegistered = _assignmentService.TryRegisterSanctuary(_registeredId, this);
        if (!_isServiceRegistered)
        {
            Unregister();
            Debug.LogError(
                $"{nameof(ExtractionSanctuary)} could not register Sanctuary {_registeredId.Value} in the assignment service.",
                this);
            return;
        }

        _isEntityRegistered = _registry.TryRegisterEntity(_registeredId, this, _registeredColliders);
        if (!_isEntityRegistered)
        {
            Unregister();
            Debug.LogError(
                $"{nameof(ExtractionSanctuary)} could not register its interactable collider for {_registeredId.Value}.",
                this);
            return;
        }

        _compositionValid = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (RitualState == ExtractionRitualState.Completed)
        {
            if (_compositionValid)
            {
                _extractionZone.TrySetAvailability(true);
            }

            return;
        }

        if (RitualState != ExtractionRitualState.InProgress)
        {
            return;
        }

        if (!_compositionValid || _extractionZone == null || _extractionZone.Id != Id || OwnerIdValue == 0 ||
            RitualTimer.IsRunning == false)
        {
            CancelRitual();
            return;
        }

        EntityId ownerId = OwnerId;
        if (_registry == null ||
            !_registry.TryGetCharacter(ownerId, out ICharacter owner) ||
            owner == null)
        {
            CancelRitual();
            return;
        }

        if (!owner.IsAlive)
        {
            CancelRitual();
            return;
        }

        if (RitualTimer.Expired(Runner))
        {
            CompleteRitual();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Unregister();
    }

    public bool IsOwnedBy(EntityId playerId)
    {
        return playerId.Value != 0 && OwnerIdValue == playerId.Value;
    }

    public bool CanUseExtraction(EntityId playerId)
    {
        return _compositionValid && IsOwnedBy(playerId) &&
            RitualState == ExtractionRitualState.Completed;
    }

    public bool CanInteract(in InteractionRequest request)
    {
        if (!_compositionValid || request.TargetId != Id || request.InteractorId.Value == 0 ||
            !IsOwnedBy(request.InteractorId) || RitualState != ExtractionRitualState.NotStarted)
        {
            return false;
        }

        return _registry != null &&
            _registry.TryGetCharacter(request.InteractorId, out ICharacter character) &&
            character != null && character.IsAlive;
    }

    public InteractionResult Interact(in InteractionRequest request)
    {
        if (!HasStateAuthority)
        {
            return InteractionResult.Rejected(InteractionFailureReason.MissingStateAuthority);
        }

        if (!CanInteract(request))
        {
            return InteractionResult.Rejected(InteractionFailureReason.TargetUnavailable);
        }

        RitualTimer = TickTimer.CreateFromSeconds(Runner, _config.RitualDurationSeconds);
        RitualState = ExtractionRitualState.InProgress;
        return InteractionResult.Succeeded(isConsumed: false);
    }

    public bool TryGetRitualProgress(out ExtractionRitualSnapshot snapshot)
    {
        if (_config == null || !_config.TryValidate(out _))
        {
            snapshot = default;
            return false;
        }

        float totalSeconds = _config.RitualDurationSeconds;
        switch (RitualState)
        {
            case ExtractionRitualState.NotStarted:
                snapshot = new ExtractionRitualSnapshot(RitualState, totalSeconds, totalSeconds, 0f);
                return true;
            case ExtractionRitualState.Completed:
                snapshot = new ExtractionRitualSnapshot(RitualState, totalSeconds, 0f, 1f);
                return true;
            case ExtractionRitualState.Cancelled:
                snapshot = new ExtractionRitualSnapshot(RitualState, totalSeconds, totalSeconds, 0f);
                return true;
            case ExtractionRitualState.InProgress:
                if (Runner == null || !RitualTimer.IsRunning)
                {
                    snapshot = default;
                    return false;
                }

                float? remaining = RitualTimer.RemainingTime(Runner);
                if (!remaining.HasValue || float.IsNaN(remaining.Value) || float.IsInfinity(remaining.Value))
                {
                    snapshot = default;
                    return false;
                }

                float remainingSeconds = Mathf.Clamp(remaining.Value, 0f, totalSeconds);
                float progress = Mathf.Clamp01((totalSeconds - remainingSeconds) / totalSeconds);
                snapshot = new ExtractionRitualSnapshot(
                    RitualState,
                    totalSeconds,
                    remainingSeconds,
                    progress);
                return true;
            default:
                snapshot = default;
                return false;
        }
    }

    /// <summary>
    /// Reserves this Sanctuary under State Authority. Repeating the same owner is idempotent;
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

    private void CompleteRitual()
    {
        if (RitualState != ExtractionRitualState.InProgress)
        {
            return;
        }

        RitualState = ExtractionRitualState.Completed;
        RitualTimer = TickTimer.None;
        _extractionZone.TrySetAvailability(true);
    }

    private void CancelRitual()
    {
        if (RitualState != ExtractionRitualState.InProgress)
        {
            return;
        }

        RitualState = ExtractionRitualState.Cancelled;
        RitualTimer = TickTimer.None;
    }

    private void CacheComposition()
    {
        if (_extractionZone == null)
        {
            _extractionZone = GetComponent<ExtractionZone>();
        }

        if (_interactionCollider == null)
        {
            _interactionCollider = GetComponent<Collider2D>();
        }

        if (_registeredColliders == null || _registeredColliders.Length != 1 ||
            _registeredColliders[0] != _interactionCollider)
        {
            _registeredColliders = _interactionCollider != null
                ? new[] { _interactionCollider }
                : null;
        }
    }

    private bool ValidateComposition()
    {
        return _config != null && _config.TryValidate(out _) &&
            _registry != null && _assignmentService != null && _registeredId.Value != 0 &&
            _extractionZone != null && _extractionZone.gameObject == gameObject && _extractionZone.Id == _registeredId &&
            _interactionCollider != null && _interactionCollider.gameObject == gameObject &&
            _registeredColliders != null && _registeredColliders.Length == 1;
    }

    private void Unregister()
    {
        if (_isEntityRegistered && _registry != null)
        {
            _registry.TryUnregisterEntity(_registeredId, this);
        }

        if (_isServiceRegistered && _assignmentService != null)
        {
            _assignmentService.TryUnregisterSanctuary(_registeredId, this);
        }

        if (_isRegistryRegistered && _registry != null)
        {
            _registry.TryUnregisterExtractionSanctuary(_registeredId, this);
        }

        _isEntityRegistered = false;
        _isServiceRegistered = false;
        _isRegistryRegistered = false;
        _compositionValid = false;
        _registeredId = default;
        _registeredColliders = null;
        _assignmentService = null;
        _registry = null;
    }

    private void OnDestroy()
    {
        Unregister();
    }

    internal int GetRestoredOwnerIdValue() => OwnerIdValue;
    internal void SetRestoredOwnerId(EntityId newOwnerId)
    {
        OwnerIdValue = newOwnerId.Value;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheComposition();
    }
#endif
}
