using System;
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

    [SerializeField]
    private RaidAvatarParticipantLink _participantLink;

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
    /// Invoked locally on peers when the extraction completes successfully.
    /// Used by integrations that require a reaction to the extraction event without polling.
    /// </summary>
    public event Action<PlayerExtractionController> ExtractionCompleted;

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

        if (HasStateAuthority && State == ExtractionState.None && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
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
        }
    }

    /// <summary>
    /// Attempts to authoritatively begin extraction in the specified zone.
    /// Implements <see cref="IExtractionParticipant.TryBeginExtraction"/>.
    /// In team-based extraction (US-53), individual countdown initiation is removed;
    /// countdown is orchestrated by <c>TeamExtractionCoordinator</c>.
    /// </summary>
    public bool TryBeginExtraction(EntityId zoneId)
    {
        return false;
    }

    /// <summary>
    /// Notifies the controller that an exit event was detected for a zone.
    /// In team-based extraction, spatial boundary tracking is handled by <c>TeamExtractionCoordinator</c>.
    /// </summary>
    public void NotifyExtractionZoneExit(EntityId zoneId)
    {
    }

    /// <summary>
    /// Initiates or synchronizes the extraction countdown for this participant as directed by <c>TeamExtractionCoordinator</c>.
    /// </summary>
    internal void StartExtractionCountdown(EntityId zoneId, TickTimer timer)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        ActiveZoneIdValue = zoneId.Value;
        ExtractionTimer = timer;
        State = ExtractionState.InProgress;
        Debug.Log($"[PlayerExtractionController] Started team extraction on {name} in Zone ID {zoneId.Value}.", this);
    }

    /// <summary>
    /// Cancels an in-progress extraction countdown as directed by <c>TeamExtractionCoordinator</c>.
    /// </summary>
    internal void CancelExtraction()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (State == ExtractionState.InProgress)
        {
            Debug.Log($"[PlayerExtractionController] Extraction CANCELLED on {name}.", this);
            State = ExtractionState.None;
            ActiveZoneIdValue = 0;
            ExtractionTimer = TickTimer.None;
        }
    }

    /// <summary>
    /// Authoritatively completes extraction as directed by <c>TeamExtractionCoordinator</c>.
    /// </summary>
    internal void CompleteExtraction()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        State = ExtractionState.Extracted;
        ExtractionTimer = TickTimer.None;
        ApplyExtractionRestrictions();
        Debug.Log($"[PlayerExtractionController] Extraction COMPLETED on {name}! Player is now EXTRACTED & Invulnerable.", this);
        _participantLink?.NotifyExtractionCompleted();
        ExtractionCompleted?.Invoke(this);
    }

    /// <summary>
    /// Calculates a progress snapshot for presentation layers.
    /// Safe and side-effect free.
    /// </summary>
    public bool TryGetProgress(out ExtractionCountdownSnapshot snapshot)
    {
        if (State == ExtractionState.None)
        {
            snapshot = ExtractionCountdownSnapshot.None();
            return true;
        }

        if (State == ExtractionState.Extracted)
        {
            snapshot = ExtractionCountdownSnapshot.Extracted(ActiveZoneId);
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

            snapshot = new ExtractionCountdownSnapshot(ExtractionState.InProgress, ActiveZoneId, remainingSeconds, totalSeconds, progress);
            return true;
        }

        snapshot = default;
        return false;
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

        if (_participantLink == null)
        {
            _participantLink = GetComponent<RaidAvatarParticipantLink>();
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
        bool hasProgress = TryGetProgress(out ExtractionCountdownSnapshot progress);
        Debug.Log($"[PlayerExtractionController] Status for {name}: State={State}, ActiveZoneId={ActiveZoneIdValue}, Progress={(hasProgress ? $"{progress.Progress * 100f:F1}%" : "N/A")}", this);
    }

    private void OnDrawGizmos()
    {
        if (State == ExtractionState.InProgress)
        {
            if (TryGetProgress(out ExtractionCountdownSnapshot progress))
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

    internal int GetRestoredActiveZoneIdValue() => ActiveZoneIdValue;
    internal void SetRestoredActiveZoneId(EntityId newZoneId)
    {
        ActiveZoneIdValue = newZoneId.Value;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
