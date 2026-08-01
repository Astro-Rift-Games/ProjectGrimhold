using Fusion;
using UnityEngine;

/// <summary>
/// Network component managing player extraction simulation ticks, process state, timer execution,
/// and revalidation rules under State Authority.
/// Implements <see cref="IExtractionParticipant"/> capability contract.
/// </summary>
/// <remarks>
/// See <c>Docs/Architecture/ExtractionArchitecture.md</c> for complete network authority and simulation details.
/// </remarks>
[DisallowMultipleComponent]
[DefaultExecutionOrder(110)]
public sealed class PlayerExtractionController : NetworkBehaviour, IExtractionParticipant
{
    [Header("Configuration")]
    [SerializeField]
    private ExtractionConfig _config;

    [Header("Dependencies")]
    [SerializeField]
    private PlayerMovementNetworkController _movementController;

    [SerializeField]
    private PlayerCombatNetworkController _combatController;

    [SerializeField]
    private MonoBehaviour _characterSource;

    private ICharacter _character;
    private EntityRegistry _entityRegistry;
    private bool _dependenciesValid;
    private bool _isRegistered;
    private EntityId _registeredId;

    /// <summary>
    /// Canonical entity identifier of the player participant.
    /// Derived from the underlying Fusion <see cref="NetworkObject"/> identifier.
    /// </summary>
    public new EntityId Id => Object != null ? new EntityId(unchecked((int)Object.Id.Raw)) : default;

    [Networked]
    public ExtractionState State { get; private set; }

    [Networked]
    private int ActiveZoneIdValue { get; set; }

    /// <summary>
    /// Gets the canonical <see cref="EntityId"/> of the active extraction zone.
    /// </summary>
    public EntityId ActiveZoneId => new EntityId(ActiveZoneIdValue);

    /// <summary>
    /// Gets the authoritative point used to validate extraction geometry.
    /// </summary>
    public Vector2 ValidationPoint => transform.position;

    [Networked]
    private TickTimer ExtractionTimer { get; set; }

    /// <summary>
    /// Categorizes extraction continuation cancellation reasons for diagnostics.
    /// </summary>
    private enum CancellationReason
    {
        None,
        InvalidZone,
        ZoneUnavailable,
        CharacterNotAlive,
        LeftZoneTolerance
    }

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        if (Runner != null)
        {
            _entityRegistry = Runner.GetComponent<EntityRegistry>();
        }

        RegisterParticipant();
        _dependenciesValid = ValidateDependencies();

        if (HasStateAuthority && State == ExtractionState.None)
        {
            ActiveZoneIdValue = 0;
            ExtractionTimer = TickTimer.None;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterParticipant();
        _dependenciesValid = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !_dependenciesValid)
        {
            return;
        }

        if (State == ExtractionState.Extracted)
        {
            ApplyExtractionRestrictions();
            return;
        }

        if (State == ExtractionState.InProgress)
        {
            if (!EvaluateContinuation(out CancellationReason reason))
            {
                CancelExtractionInternal(reason);
                return;
            }

            if (ExtractionTimer.Expired(Runner))
            {
                CompleteExtractionInternal();
            }
        }
    }

    /// <summary>
    /// Attempts to authoritatively begin extraction in the specified zone.
    /// Implements <see cref="IExtractionParticipant.TryBeginExtraction"/>.
    /// </summary>
    /// <param name="zoneId">The canonical ID of the target extraction zone.</param>
    /// <returns><see langword="true"/> if extraction was initiated; otherwise, <see langword="false"/>.</returns>
    public bool TryBeginExtraction(EntityId zoneId)
    {
        if (!HasStateAuthority || !_dependenciesValid)
        {
            return false;
        }

        if (State != ExtractionState.None)
        {
            return false;
        }

        if (zoneId.Value == 0)
        {
            return false;
        }

        if (!_entityRegistry.TryGetExtractionZone(zoneId, out IExtractionZone zone) || !zone.IsAvailable)
        {
            return false;
        }

        if (!zone.ContainsExact(ValidationPoint))
        {
            return false;
        }

        if (_config.RequireAliveToStart && (_character == null || !_character.IsAlive))
        {
            return false;
        }

        ActiveZoneIdValue = zoneId.Value;
        ExtractionTimer = TickTimer.CreateFromSeconds(Runner, _config.CountdownDurationSeconds);
        State = ExtractionState.InProgress;
        Debug.Log($"[PlayerExtractionController] Started extraction on {name} in Zone ID {zoneId.Value} ({_config.CountdownDurationSeconds}s countdown).", this);
        return true;
    }

    /// <summary>
    /// Notifies the controller that an exit event was detected for a zone.
    /// Re-evaluates continuation policy before modifying extraction state.
    /// Implements <see cref="IExtractionParticipant.NotifyExtractionZoneExit"/>.
    /// </summary>
    /// <param name="zoneId">Target zone ID associated with the exit notification.</param>
    public void NotifyExtractionZoneExit(EntityId zoneId)
    {
        if (!HasStateAuthority || !_dependenciesValid)
        {
            return;
        }

        if (State != ExtractionState.InProgress)
        {
            return;
        }

        if (zoneId != ActiveZoneId)
        {
            return;
        }

        if (!_config.CancelWhenLeavingArea)
        {
            return;
        }

        if (!EvaluateContinuation(out CancellationReason reason))
        {
            CancelExtractionInternal(reason);
        }
    }

    /// <summary>
    /// Evaluates continuation validity without applying state side effects.
    /// </summary>
    /// <param name="reason">Outputs the cancellation reason if invalid; otherwise <see cref="CancellationReason.None"/>.</param>
    /// <returns><see langword="true"/> if extraction can continue; otherwise, <see langword="false"/>.</returns>
    private bool EvaluateContinuation(out CancellationReason reason)
    {
        reason = CancellationReason.None;

        if (ActiveZoneId.Value == 0)
        {
            reason = CancellationReason.InvalidZone;
            return false;
        }

        IExtractionZone zone = null;
        if (_entityRegistry == null ||
            !_entityRegistry.TryGetExtractionZone(ActiveZoneId, out zone) ||
            zone == null ||
            !zone.IsAvailable)
        {
            reason = zone == null ? CancellationReason.InvalidZone : CancellationReason.ZoneUnavailable;
            return false;
        }

        if (_config.CancelWhenNotAlive && (_character == null || !_character.IsAlive))
        {
            reason = CancellationReason.CharacterNotAlive;
            return false;
        }

        if (_config.CancelWhenLeavingArea && !zone.ContainsWithTolerance(ValidationPoint, _config.BoundaryTolerance))
        {
            reason = CancellationReason.LeftZoneTolerance;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Calculates a progress snapshot for presentation layers.
    /// Safe and side-effect free.
    /// </summary>
    public bool TryGetProgress(out ExtractionProgressSnapshot snapshot)
    {
        if (State == ExtractionState.None)
        {
            snapshot = ExtractionProgressSnapshot.None();
            return true;
        }

        if (State == ExtractionState.Extracted)
        {
            snapshot = ExtractionProgressSnapshot.Extracted(ActiveZoneId);
            return true;
        }

        if (State == ExtractionState.InProgress)
        {
            if (Runner == null || _config == null || ActiveZoneId.Value == 0 || ExtractionTimer.IsRunning == false)
            {
                snapshot = default;
                return false;
            }

            float totalSeconds = _config.CountdownDurationSeconds;
            float? remainingTime = ExtractionTimer.RemainingTime(Runner);
            if (!remainingTime.HasValue || float.IsNaN(remainingTime.Value) || float.IsInfinity(remainingTime.Value))
            {
                snapshot = default;
                return false;
            }

            float remainingSeconds = remainingTime.Value;
            float progress = totalSeconds > 0f ? Mathf.Clamp01((totalSeconds - remainingSeconds) / totalSeconds) : 1f;

            snapshot = new ExtractionProgressSnapshot(ExtractionState.InProgress, ActiveZoneId, remainingSeconds, totalSeconds, progress);
            return true;
        }

        snapshot = default;
        return false;
    }

    private void CancelExtractionInternal(CancellationReason reason)
    {
        Debug.Log($"[PlayerExtractionController] Extraction CANCELLED on {name} (Reason: {reason}).", this);
        State = ExtractionState.None;
        ActiveZoneIdValue = 0;
        ExtractionTimer = TickTimer.None;
    }

    private void CompleteExtractionInternal()
    {
        State = ExtractionState.Extracted;
        ExtractionTimer = TickTimer.None;
        ApplyExtractionRestrictions();
        Debug.Log($"[PlayerExtractionController] Extraction COMPLETED on {name}! Player is now EXTRACTED & Invulnerable.", this);
    }

    private void ApplyExtractionRestrictions()
    {
        if (_movementController != null)
        {
            _movementController.TrySetControlEnabled(false);
        }

        if (_combatController != null)
        {
            _combatController.TrySetAttackEnabled(false);
        }
    }

    private void CacheDependencies()
    {
        if (_movementController == null)
        {
            _movementController = GetComponent<PlayerMovementNetworkController>();
        }

        if (_combatController == null)
        {
            _combatController = GetComponent<PlayerCombatNetworkController>();
        }

        if (_characterSource != null)
        {
            _character = _characterSource as ICharacter;
        }

        if (_character == null)
        {
            _character = GetComponent<ICharacter>() ?? GetComponentInParent<ICharacter>();
        }
    }

    private bool ValidateDependencies()
    {
        if (_config == null)
        {
            Debug.LogError($"{nameof(PlayerExtractionController)} requires an assigned {nameof(ExtractionConfig)} asset.", this);
            return false;
        }

        if (_config.TryValidate(out string error) == false)
        {
            Debug.LogError(error, this);
            return false;
        }

        if (_movementController == null)
        {
            Debug.LogError($"{nameof(PlayerExtractionController)} requires {nameof(PlayerMovementNetworkController)} on the player object.", this);
            return false;
        }

        if (_combatController == null)
        {
            Debug.LogError($"{nameof(PlayerExtractionController)} requires {nameof(PlayerCombatNetworkController)} on the player object.", this);
            return false;
        }

        if (_character == null)
        {
            Debug.LogError($"{nameof(PlayerExtractionController)} requires a component implementing {nameof(ICharacter)}.", this);
            return false;
        }

        if (_entityRegistry == null)
        {
            Debug.LogError($"{nameof(PlayerExtractionController)} requires a runner-scoped {nameof(EntityRegistry)}.", this);
            return false;
        }

        if (!_isRegistered)
        {
            Debug.LogError($"{nameof(PlayerExtractionController)} could not register its extraction participant capability.", this);
            return false;
        }

        return true;
    }

    private void RegisterParticipant()
    {
        if (_isRegistered || _entityRegistry == null || Id.Value == 0)
        {
            return;
        }

        _registeredId = Id;
        _isRegistered = _entityRegistry.TryRegisterExtractionParticipant(_registeredId, this);
    }

    private void UnregisterParticipant()
    {
        if (!_isRegistered)
        {
            return;
        }

        if (_entityRegistry != null)
        {
            _entityRegistry.TryUnregisterExtractionParticipant(_registeredId, this);
        }

        _registeredId = default;
        _isRegistered = false;
    }

    private void OnDestroy()
    {
        UnregisterParticipant();
    }

    [ContextMenu("Debug: Log Extraction Status")]
    private void DebugLogStatus()
    {
        bool hasProgress = TryGetProgress(out ExtractionProgressSnapshot progress);
        bool canContinue = EvaluateContinuation(out CancellationReason reason);
        Debug.Log($"[PlayerExtractionController] Status for {name}: State={State}, ActiveZoneId={ActiveZoneIdValue}, CanContinue={canContinue} (Reason={reason}), Progress={(hasProgress ? $"{progress.Progress * 100f:F1}%" : "N/A")}", this);
    }

    private void OnDrawGizmos()
    {
        if (State == ExtractionState.InProgress)
        {
            if (TryGetProgress(out ExtractionProgressSnapshot progress))
            {
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
                Gizmos.DrawWireSphere(transform.position, 0.8f);
                Gizmos.DrawSphere(transform.position, 0.8f * progress.Progress);
            }
        }
        else if (State == ExtractionState.Extracted)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 1.0f);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
