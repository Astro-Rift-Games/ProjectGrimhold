using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Network component responsible for processing player interaction intentions.
/// Delegates targets validation and execution to the pure logical resolver.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerInteractionNetworkController : NetworkBehaviour, IAfterTick
{
    public static readonly LootId LockpickLootId = new("CON_LOCKPICK");

    [Header("Lockpicking")]
    [SerializeField, Min(0.01f)]
    private float _lockpickDurationSeconds = 3f;

    [Header("Dependencies")]
    [SerializeField]
    private PlayerInteractionConfig _config;

    [SerializeField]
    private MonoBehaviour _characterSource;

    [SerializeField]
    private MonoBehaviour _querySource;

    [SerializeField]
    private PlayerMovementNetworkController _movementController;

    [SerializeField]
    private Transform _interactionOrigin;

    private ICharacter _character;
    private IInteractionTargetQuery _query;
    private EntityRegistry _registry;
    private PlayerExtractionController _extractionController;
    private NetworkMatchController _matchController;
    private PlayerAbilityRuntimeNetworkController _abilityRuntime;
    private PlayerDownedRecoveryNetworkController _downedRecovery;
    private PlayerCombatNetworkController _combat;
    private PlayerLootReceiver _lootReceiver;
    private bool _dependenciesValid;
    private bool _spawned;
    private bool _cancelRestoredLockpick;
    private readonly Queue<InteractionPresentationEvent> _pendingPresentationEvents = new();

    [Networked]
    private NetworkButtons PreviousButtons { get; set; }

    [Networked] private TickTimer LockpickTimer { get; set; }
    [Networked] private int LockpickTargetIdValue { get; set; }
    [Networked] private Vector2 LockpickStartPosition { get; set; }

    public bool IsLockpicking => _spawned && Object != null && Object.IsValid && LockpickTimer.IsRunning;
    public EntityId LockpickTargetId => new(LockpickTargetIdValue);
    public float LockpickDurationSeconds => _lockpickDurationSeconds;
    public float LockpickRemainingSeconds => IsLockpicking ? LockpickTimer.RemainingTime(Runner) ?? 0f : 0f;

    /// <summary>
    /// True while the Interact input is held. Published by State Authority so a recovery session
    /// running on another avatar can cancel when the reviver releases the button.
    /// </summary>
    [Networked]
    public NetworkBool IsInteractHeld { get; private set; }

    [Networked]
    private int InteractionSequence { get; set; }

    [Networked]
    private int LastInteractionTargetIdValue { get; set; }

    [Networked]
    private int LastInteractionTick { get; set; }

    [Networked]
    private NetworkBool LastInteractionSucceeded { get; set; }

    [Networked]
    private NetworkBool LastInteractionConsumed { get; set; }

    [Networked]
    private int LastInteractionFailureReasonValue { get; set; }

    /// <summary>
    /// Gets the latest authoritative interaction attempt sequence.
    /// Presentation uses it only to establish a non-replaying baseline.
    /// </summary>
    public int CurrentInteractionSequence => InteractionSequence;

    /// <summary>
    /// Local event raised during Render for each authoritative interaction result
    /// delivered to this object's Input Authority.
    /// </summary>
    public event Action<InteractionPresentationEvent> InteractionResolved;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _dependenciesValid = ValidateDependencies();

        _registry = Runner.GetComponent<EntityRegistry>();
        _matchController = Runner.GetComponent<NetworkMatchController>();
        _spawned = true;
        // EntityIds are reassigned during migration. Never resolve a copied target ID
        // before explicit remapping; interrupted contextual work can safely be retried.
        _cancelRestoredLockpick = HasStateAuthority && HostMigrationRestoreUtility.IsRestoreSpawn(this) &&
            LockpickTimer.IsRunning;
        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            LockpickTimer = TickTimer.None;
            LockpickTargetIdValue = 0;
        }
        if (_registry == null)
        {
            Debug.LogError($"{nameof(PlayerInteractionNetworkController)}: EntityRegistry was not found on the NetworkRunner.", this);
            _dependenciesValid = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !Runner.IsForward)
        {
            return;
        }

        if (!_dependenciesValid)
        {
            return;
        }

        if (_matchController != null &&
            _matchController.Phase != NetworkMatchController.MatchPhase.InProgress)
        {
            PreviousButtons = default;
            IsInteractHeld = false;
            CancelLockpicking();
            return;
        }

        bool hasInput = GetInput(out PlayerNetworkInput input);
        bool wasLockpicking = IsLockpicking;
        if (!hasInput)
        {
            return;
        }

        NetworkButtons currentButtons = input.Buttons;
        IsInteractHeld = currentButtons.IsSet(PlayerInputButton.Interact);
        bool interactPressed = currentButtons.WasPressed(PreviousButtons, PlayerInputButton.Interact);

        PreviousButtons = currentButtons;

        if (wasLockpicking)
        {
            return;
        }

        if (!interactPressed)
        {
            return;
        }

        // A Downed player cannot interact, but Interact is the contextual Self-revive input (GD 13 section 7).
        if (PlayerDownedGate.IsDowned(_character) && _downedRecovery != null && _downedRecovery.TryBeginSelf())
        {
            return;
        }

        if (_character == null || !_character.IsAlive || (PlayerDownedGate.IsDowned(_character) || PlayerConsumptionGate.IsConsuming(_character)) ||
            (_abilityRuntime != null && _abilityRuntime.HasActiveExecution) ||
            (_extractionController != null && _extractionController.State == ExtractionState.Extracted))
        {
            RecordInteractionResult(default, Runner.Tick, InteractionResult.Rejected(InteractionFailureReason.InteractorUnavailable));
            return;
        }

        if (_movementController == null || !_movementController.IsControlEnabled)
        {
            RecordInteractionResult(default, Runner.Tick, InteractionResult.Rejected(InteractionFailureReason.InteractionDisabled));
            return;
        }

        PlayerReviveGate.InterruptIfReviving(_character);
        TryProcessInteraction();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _spawned = false;
        _pendingPresentationEvents.Clear();
    }

    public void AfterTick()
    {
        if (!_spawned || !HasStateAuthority || Runner == null || !Runner.IsForward || !IsLockpicking) return;
        if (_cancelRestoredLockpick)
        {
            _cancelRestoredLockpick = false;
            CancelLockpicking();
            return;
        }
        // Run after all simulation callbacks: movement or committed damage on the deadline tick
        // must interrupt before success, without reordering the existing Interact/revive pipeline.
        bool hasInput = GetInput(out PlayerNetworkInput input);
        AdvanceLockpicking(hasInput ? input.MoveDirection : Vector2.zero);
    }

    /// <summary>State Authority starts the contextual action without opening or spending anything.</summary>
    public InteractionResult TryBeginLockpicking(NetworkLootContainer container, in InteractionRequest request)
    {
        if (!_spawned || !HasStateAuthority || Runner == null || !Runner.IsForward)
            return InteractionResult.Rejected(InteractionFailureReason.MissingStateAuthority);
        if (IsLockpicking || !CanLockpick() || request.InteractorId != _character.Id ||
            container == null || container.Id != request.TargetId || !container.IsLocked ||
            !TryResolveLockpickTarget(request.TargetId, out _, out _))
            return InteractionResult.Rejected(InteractionFailureReason.InteractorUnavailable);
        if (_lootReceiver.GetLootAmount(LockpickLootId) < 1)
            return InteractionResult.Rejected(InteractionFailureReason.LockpickRequired);
        if (GetInput(out PlayerNetworkInput input) &&
            LockpickChannelRules.HasMoved(transform.position, transform.position, input.MoveDirection))
            return InteractionResult.Rejected(InteractionFailureReason.InteractorUnavailable);

        LockpickStartPosition = transform.position;
        LockpickTargetIdValue = request.TargetId.Value;
        LockpickTimer = TickTimer.CreateFromSeconds(Runner, _lockpickDurationSeconds);
        return InteractionResult.Rejected(InteractionFailureReason.LockpickingStarted);
    }

    /// <summary>Direct committed-damage hook. Never called by presentation events.</summary>
    public void CancelLockpicking()
    {
        if (!_spawned || !HasStateAuthority || Runner == null || !Runner.IsForward || !IsLockpicking) return;
        EntityId targetId = LockpickTargetId;
        ClearLockpickChannel();
        RecordInteractionResult(targetId, Runner.Tick,
            InteractionResult.Rejected(InteractionFailureReason.InteractorUnavailable));
    }

    private bool CanLockpick() => _dependenciesValid && _lootReceiver != null &&
        LockpickChannelRules.IsValidDuration(_lockpickDurationSeconds) &&
        (_matchController == null || _matchController.Phase == NetworkMatchController.MatchPhase.InProgress) &&
        _character != null && _character.IsAlive && !PlayerDownedGate.IsDowned(_character) &&
        !PlayerConsumptionGate.IsConsuming(_character) &&
        (_abilityRuntime == null || !_abilityRuntime.HasActiveExecution) &&
        (_combat == null || (!_combat.IsExecutingWeaponAttack && !_combat.HasPendingAttackRelease)) &&
        (_downedRecovery == null || (!_downedRecovery.HasValidSession && !_downedRecovery.IsRevivingAnotherAvatar)) &&
        (_extractionController == null || _extractionController.State != ExtractionState.Extracted) &&
        _movementController != null && _movementController.IsControlEnabled;

    private void AdvanceLockpicking(Vector2 movementIntent)
    {
        EntityId targetId = LockpickTargetId;
        // Cancellation always wins over a deadline reached on this tick.
        if (!CanLockpick() ||
            LockpickChannelRules.HasMoved(LockpickStartPosition, transform.position, movementIntent) ||
            !TryResolveLockpickTarget(targetId, out NetworkLootContainer container, out IInteractable interactable) ||
            !container.IsLocked || _lootReceiver.GetLootAmount(LockpickLootId) < 1)
        {
            CancelLockpicking();
            return;
        }
        if (!LockpickTimer.Expired(Runner)) return;

        var extraction = new LootTransferRequest(_character.Id, _character.Id, LockpickLootId, 1, Runner.Tick);
        if (_lootReceiver.ValidateExtraction(extraction) != LootTransferFailureReason.None ||
            !_lootReceiver.TryResolveRaidLootOriginTransfer(extraction, RaidLootOriginSelectionOrder.PlayerFirst,
                out RaidLootOriginTransfer origins))
        {
            CancelLockpicking();
            return;
        }

        // Every fallible precondition is resolved first. These synchronous commits do not yield,
        // call presentation or permit a competing mutation; a failed unlock never spends a pick.
        if (!container.TryUnlock())
        {
            CancelLockpicking();
            return;
        }
        _lootReceiver.CommitRaidLootExtraction(extraction, origins);
        ClearLockpickChannel();
        var request = new InteractionRequest(_character.Id, targetId, Runner.Tick);
        RecordInteractionResult(targetId, Runner.Tick, interactable.Interact(request));
    }

    private bool TryResolveLockpickTarget(EntityId targetId, out NetworkLootContainer container,
        out IInteractable interactable)
    {
        container = null;
        interactable = null;
        if (_registry == null || !_registry.TryGetInteractionHandler(targetId, out interactable) ||
            interactable is not NetworkLootContainerInteractable adapter ||
            !adapter.TryGetComponent(out container)) return false;
        var request = new InteractionRequest(_character.Id, targetId, Runner.Tick);
        if (!adapter.CanInteract(request)) return false;
        Vector2 origin = _interactionOrigin != null ? _interactionOrigin.position : transform.position;
        var query = new InteractionTargetQuery(_character.Id, origin, _config.MaximumDistance, _config.TargetLayerMask);
        var candidates = _query.FindTargets(query);
        for (int index = 0; index < candidates.Count; index++)
        {
            if (candidates[index].TargetId == targetId && candidates[index].Distance <= _config.MaximumDistance)
                return true;
        }
        return false;
    }

    private void ClearLockpickChannel()
    {
        _cancelRestoredLockpick = false;
        LockpickTimer = TickTimer.None;
        LockpickTargetIdValue = 0;
    }

    private void TryProcessInteraction()
    {
        Vector2 originPos = _interactionOrigin != null ? (Vector2)_interactionOrigin.position : (Vector2)transform.position;

        InteractionTargetQuery targetQuery = new InteractionTargetQuery(
            _character.Id,
            originPos,
            _config.MaximumDistance,
            _config.TargetLayerMask
        );

        var candidates = _query.FindTargets(in targetQuery);

        bool executed = InteractionResolver.TryResolve(
            _character.Id,
            Runner.Tick,
            _config.MaximumDistance,
            candidates,
            _registry.TryGetInteractionHandler,
            out var resolvedRequest,
            out var resolvedResult
        );

        if (!executed)
        {
            RecordInteractionResult(default, Runner.Tick, InteractionResult.Rejected(InteractionFailureReason.InvalidTarget));
            return;
        }

        RecordInteractionResult(resolvedRequest.TargetId, resolvedRequest.SimulationTick, resolvedResult);
    }

    public override void Render()
    {
        if (!HasInputAuthority)
        {
            _pendingPresentationEvents.Clear();
            return;
        }

        // Dispatch only the events that existed when this render pass began. This keeps
        // presentation callbacks from extending the same dispatch pass re-entrantly.
        int pendingCount = _pendingPresentationEvents.Count;
        for (int index = 0; index < pendingCount; index++)
        {
            // Dequeue first: `?.` skips argument evaluation when nothing is subscribed.
            InteractionPresentationEvent interactionEvent = _pendingPresentationEvents.Dequeue();
            InteractionResolved?.Invoke(interactionEvent);
        }
    }

    private void RecordInteractionResult(EntityId targetId, int simulationTick, in InteractionResult result)
    {
        InteractionSequence++;
        LastInteractionTargetIdValue = targetId.Value;
        LastInteractionTick = simulationTick;
        LastInteractionSucceeded = result.Success;
        LastInteractionConsumed = result.IsConsumed;
        LastInteractionFailureReasonValue = (int)result.FailureReason;

        if (InteractionResultDeliveryPolicy.ShouldEnqueueLocally(HasStateAuthority, HasInputAuthority))
        {
            EnqueueInteractionResult(
                InteractionSequence,
                targetId.Value,
                simulationTick,
                result.Success,
                result.IsConsumed,
                (int)result.FailureReason,
                (int)result.LootFailureReason);
            return;
        }

        if (InteractionResultDeliveryPolicy.ShouldSendRemote(HasStateAuthority, HasInputAuthority))
        {
            RPC_ReceiveInteractionResult(
                InteractionSequence,
                targetId.Value,
                simulationTick,
                result.Success,
                result.IsConsumed,
                (int)result.FailureReason,
                (int)result.LootFailureReason);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, InvokeLocal = false)]
    private void RPC_ReceiveInteractionResult(
        int sequence,
        int targetIdValue,
        int simulationTick,
        bool succeeded,
        bool consumed,
        int failureReasonValue,
        int lootFailureReasonValue)
    {
        EnqueueInteractionResult(
            sequence,
            targetIdValue,
            simulationTick,
            succeeded,
            consumed,
            failureReasonValue,
            lootFailureReasonValue);
    }

    private void EnqueueInteractionResult(
        int sequence,
        int targetIdValue,
        int simulationTick,
        bool succeeded,
        bool consumed,
        int failureReasonValue,
        int lootFailureReasonValue)
    {
        if (!HasInputAuthority)
        {
            return;
        }

        _pendingPresentationEvents.Enqueue(new InteractionPresentationEvent(
            sequence,
            _character != null ? _character.Id : default,
            new EntityId(targetIdValue),
            simulationTick,
            succeeded,
            consumed,
            (InteractionFailureReason)failureReasonValue,
            (LootTransferFailureReason)lootFailureReasonValue));
    }

    private void CacheDependencies()
    {
        _abilityRuntime = GetComponent<PlayerAbilityRuntimeNetworkController>();
        _downedRecovery = GetComponent<PlayerDownedRecoveryNetworkController>();
        _combat = GetComponent<PlayerCombatNetworkController>();
        _lootReceiver = GetComponent<PlayerLootReceiver>();
        if (_characterSource != null)
        {
            _character = _characterSource as ICharacter;
        }
        else
        {
            _character = GetComponent<ICharacter>();
        }

        if (_querySource != null)
        {
            _query = _querySource as IInteractionTargetQuery;
        }
        else
        {
            _query = GetComponent<IInteractionTargetQuery>();
        }

        if (_movementController == null)
        {
            _movementController = GetComponent<PlayerMovementNetworkController>();
        }

        if (_interactionOrigin == null)
        {
            _interactionOrigin = transform;
        }

        if (_extractionController == null)
        {
            _extractionController = GetComponent<PlayerExtractionController>();
        }
    }

    private bool ValidateDependencies()
    {
        if (_config == null)
        {
            Debug.LogError($"{nameof(PlayerInteractionNetworkController)}: Configuration asset is not assigned.", this);
            return false;
        }

        if (_character == null)
        {
            Debug.LogError($"{nameof(PlayerInteractionNetworkController)}: No component implementing {nameof(ICharacter)} is assigned.", this);
            return false;
        }

        if (_query == null)
        {
            Debug.LogError($"{nameof(PlayerInteractionNetworkController)}: No component implementing {nameof(IInteractionTargetQuery)} is assigned.", this);
            return false;
        }

        if (_movementController == null)
        {
            Debug.LogError($"{nameof(PlayerInteractionNetworkController)}: PlayerMovementNetworkController is not assigned.", this);
            return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
