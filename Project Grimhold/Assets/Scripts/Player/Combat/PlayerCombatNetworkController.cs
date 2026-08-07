using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Network component responsible for processing player attack intentions
/// and delegating execution to the active attack strategy.
///
/// This controller operates with any strategy implementing the
/// <see cref="IAttack"/> contract, isolating gameplay simulation from visual presentation.
/// </summary>
[DisallowMultipleComponent]
// Movement writes the final-tick FacingDirection before combat consumes it.
[DefaultExecutionOrder(-9)]
public sealed class PlayerCombatNetworkController : NetworkBehaviour,
    ICombatController,
    IResolvedDamageFeedbackSink
{
    [Header("Dependencies")]
    [SerializeField]
    private MonoBehaviour _characterSource;

    [SerializeField]
    private Transform _attackOrigin;

    [SerializeField]
    private MonoBehaviour _activeAttackSource;

    [SerializeField]
    private PlayerMovementNetworkController _movementController;

    private ICharacter _character;
    private IAttack _activeAttack;
    private bool _dependenciesValid;
    private int _lastObservedSequence;
    private readonly Queue<CombatPresentationEvent> _pendingFeedbackEvents = new();

    [Networked]
    private NetworkButtons PreviousButtons { get; set; }

    [Networked]
    private TickTimer AttackCooldown { get; set; }

    [Networked]
    public NetworkBool IsAttackEnabled { get; private set; }

    // Replicated state for local presentation layers
    [Networked]
    private int AttackSequence { get; set; }

    [Networked]
    private Vector2 LastAttackOrigin { get; set; }

    [Networked]
    private Vector2 LastAttackDirection { get; set; }

    [Networked]
    private int LastAttackTypeValue { get; set; }

    [Networked]
    private int LastAttackTick { get; set; }

    [Networked]
    private int CombatFeedbackSequence { get; set; }

    /// <summary>
    /// Local event raised during Render when a successful attack execution is detected in the simulation.
    /// </summary>
    public event Action<AttackPerformedEvent> AttackPerformed;

    /// <summary>
    /// Local event raised during Render for authoritative combat feedback addressed
    /// to this object's Input Authority.
    /// </summary>
    public event Action<CombatPresentationEvent> CombatFeedbackResolved;

    /// <summary>
    /// Gets the latest authoritative feedback sequence for non-replaying presenter binding.
    /// </summary>
    public int CurrentCombatFeedbackSequence => CombatFeedbackSequence;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _dependenciesValid = ValidateDependencies();

        // Initialize the local observed sequence with the current network sequence
        // to prevent triggering events from attacks performed before this proxy spawned.
        _lastObservedSequence = AttackSequence;
        _pendingFeedbackEvents.Clear();

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            IsAttackEnabled = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!_dependenciesValid)
        {
            return;
        }

        // Read input from Fusion. If no input is available for this tick, exit immediately.
        if (!GetInput(out PlayerNetworkInput input))
        {
            return;
        }

        NetworkButtons currentButtons = input.Buttons;
        bool attackPressedThisTick = currentButtons.WasPressed(
            PreviousButtons,
            PlayerInputButton.PrimaryAttack);
        bool attackPressed = false;

        if (_activeAttack != null)
        {
            if (_activeAttack.InputMode == AttackInputMode.Press)
            {
                attackPressed = attackPressedThisTick;
            }
            else
            {
                attackPressed = currentButtons.IsSet(PlayerInputButton.PrimaryAttack);
            }
        }

        // Save previous buttons state even if combat is disabled or on cooldown,
        // to prevent interpreting an old press when combat gets re-enabled.
        PreviousButtons = currentButtons;

        // Only State Authority decides and executes the authoritative attack strategy.
        if (!HasStateAuthority)
        {
            return;
        }

        if (!attackPressed)
        {
            return;
        }

        AttackFailureReason prerequisiteFailure = GetPrimaryAttackFailureReason();
        if (prerequisiteFailure != AttackFailureReason.None)
        {
            if (attackPressedThisTick && prerequisiteFailure == AttackFailureReason.CooldownActive)
            {
                RecordCombatFeedback(
                    CombatFeedbackKind.AttackRejected,
                    default,
                    default,
                    0f,
                    Runner.Tick,
                    prerequisiteFailure);
            }
            return;
        }

        TryExecuteAttack();
    }

    public override void Render()
    {
        if (!_dependenciesValid)
        {
            return;
        }

        // Detect changes in the attack sequence to notify the local presentation layer
        if (AttackSequence != _lastObservedSequence)
        {
            AttackPerformedEvent performedEvent = new AttackPerformedEvent(
                _character.Id,
                (AttackType)LastAttackTypeValue,
                LastAttackOrigin,
                LastAttackDirection,
                LastAttackTick
            );

            AttackPerformed?.Invoke(performedEvent);
            _lastObservedSequence = AttackSequence;
        }

        if (_character != null && !_character.IsAlive)
        {
            _pendingFeedbackEvents.Clear();
            return;
        }

        while (_pendingFeedbackEvents.Count > 0)
        {
            CombatFeedbackResolved?.Invoke(_pendingFeedbackEvents.Dequeue());
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _pendingFeedbackEvents.Clear();
    }

    /// <summary>
    /// Reads the current primary-attack availability and cooldown for presentation.
    /// This query has no simulation side effects and is valid on replicated proxies.
    /// </summary>
    public bool TryGetPrimaryAttackStatus(out PrimaryAttackStatus status)
    {
        status = default;
        if (Runner == null || !Runner.IsRunning || !_dependenciesValid ||
            _character == null || _activeAttack == null)
        {
            return false;
        }

        float durationSeconds = _activeAttack.CooldownSeconds;
        float remainingSeconds = AttackCooldown.RemainingTime(Runner) ?? 0f;
        status = new PrimaryAttackStatus(
            GetPrimaryAttackFailureReason() == AttackFailureReason.None,
            durationSeconds,
            remainingSeconds);
        return true;
    }

    /// <summary>
    /// Records an exact resolved damage result for local attacker feedback.
    /// Rejected or zero-damage results never become confirmed impacts.
    /// </summary>
    public void RecordResolvedDamage(in DamageResolvedEvent resolvedDamage)
    {
        if (!HasStateAuthority || _character == null ||
            resolvedDamage.Request.AttackerId != _character.Id ||
            !resolvedDamage.Result.IsApplied ||
            resolvedDamage.Result.AppliedDamage <= 0f)
        {
            return;
        }

        RecordCombatFeedback(
            CombatFeedbackKind.ConfirmedImpact,
            resolvedDamage.Result.TargetId,
            resolvedDamage.Request.HitPoint,
            resolvedDamage.Result.AppliedDamage,
            resolvedDamage.Request.SimulationTick,
            AttackFailureReason.None);
    }

    /// <summary>
    /// Attempts to execute the active attack after shared readiness prerequisites
    /// have been evaluated by the authoritative simulation flow.
    /// </summary>
    private void TryExecuteAttack()
    {
        if (_movementController == null)
        {
            return;
        }

        Vector2 originPos = _attackOrigin != null
            ? (Vector2)_attackOrigin.position
            : (Vector2)transform.position;

        if (!PlayerAimMath.TryNormalizeDirection(
                _movementController.FacingDirection,
                out Vector2 direction))
        {
            return;
        }

        AttackRequest request = new AttackRequest(
            _character.Id,
            originPos,
            direction,
            (int)Runner.Tick
        );

        AttackResult result = _activeAttack.Execute(in request);

        if (result.WasExecuted)
        {
            float cooldownSeconds = _activeAttack.CooldownSeconds;
            if (cooldownSeconds > 0f)
            {
                AttackCooldown = TickTimer.CreateFromSeconds(Runner, cooldownSeconds);
            }
            else
            {
                AttackCooldown = TickTimer.None;
            }

            LastAttackOrigin = request.Origin;
            LastAttackDirection = request.Direction;
            LastAttackTypeValue = (int)_activeAttack.Type;
            LastAttackTick = request.SimulationTick;
            
            // Increment sequence last to ensure correct replication of all related fields
            AttackSequence++;
        }
    }

    private AttackFailureReason GetPrimaryAttackFailureReason()
    {
        if (Runner == null || !Runner.IsRunning || !_dependenciesValid ||
            _character == null || _activeAttack == null)
        {
            return AttackFailureReason.MissingConfiguration;
        }

        if (!IsAttackEnabled || !_character.IsAlive)
        {
            return AttackFailureReason.ControlDisabled;
        }

        return AttackCooldown.ExpiredOrNotRunning(Runner)
            ? AttackFailureReason.None
            : AttackFailureReason.CooldownActive;
    }

    private void RecordCombatFeedback(
        CombatFeedbackKind kind,
        EntityId targetId,
        Vector2 hitPoint,
        float appliedDamage,
        int simulationTick,
        AttackFailureReason failureReason)
    {
        CombatFeedbackSequence++;
        RPC_ReceiveCombatFeedback(
            CombatFeedbackSequence,
            (int)kind,
            targetId.Value,
            hitPoint,
            appliedDamage,
            simulationTick,
            (int)failureReason);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveCombatFeedback(
        int sequence,
        int kindValue,
        int targetIdValue,
        Vector2 hitPoint,
        float appliedDamage,
        int simulationTick,
        int failureReasonValue)
    {
        _pendingFeedbackEvents.Enqueue(new CombatPresentationEvent(
            sequence,
            (CombatFeedbackKind)kindValue,
            new EntityId(targetIdValue),
            hitPoint,
            appliedDamage,
            simulationTick,
            (AttackFailureReason)failureReasonValue));
    }

    /// <summary>
    /// Authortatively changes the combat enabled state.
    /// </summary>
    public bool TrySetAttackEnabled(bool enabled)
    {
        if (!HasStateAuthority)
        {
            return false;
        }

        IsAttackEnabled = enabled;
        return true;
    }

    /// <summary>
    /// Authortatively updates the active attack strategy. Requires State Authority.
    /// </summary>
    public bool TrySetActiveAttack(MonoBehaviour attackSource)
    {
        if (!HasStateAuthority)
        {
            return false;
        }

        if (attackSource == null)
        {
            Debug.LogError($"{nameof(PlayerCombatNetworkController)}: Cannot set active attack to null.", this);
            return false;
        }

        if (attackSource is IAttack newAttack)
        {
            _activeAttackSource = attackSource;
            _activeAttack = newAttack;
            return true;
        }

        Debug.LogError($"{nameof(PlayerCombatNetworkController)}: The component {attackSource.name} does not implement {nameof(IAttack)}.", this);
        return false;
    }

    private void CacheDependencies()
    {
        if (_characterSource != null)
        {
            _character = _characterSource as ICharacter;
        }

        if (_character == null)
        {
            _character = GetComponent<ICharacter>() ?? GetComponentInParent<ICharacter>();
        }

        if (_activeAttackSource != null)
        {
            _activeAttack = _activeAttackSource as IAttack;
        }

        if (_activeAttack == null)
        {
            _activeAttack = GetComponent<IAttack>() ?? GetComponentInChildren<IAttack>();
            if (_activeAttack is MonoBehaviour attackMb)
            {
                _activeAttackSource = attackMb;
            }
        }

        if (_attackOrigin == null)
        {
            _attackOrigin = transform;
        }

        if (_movementController == null)
        {
            _movementController = GetComponent<PlayerMovementNetworkController>();
        }
    }

    private bool ValidateDependencies()
    {
        if (_character == null)
        {
            Debug.LogError($"{nameof(PlayerCombatNetworkController)} requires a component implementing {nameof(ICharacter)}.", this);
            return false;
        }

        if (_attackOrigin == null)
        {
            Debug.LogError($"{nameof(PlayerCombatNetworkController)} requires an assigned {nameof(_attackOrigin)} Transform.", this);
            return false;
        }

        if (_activeAttack == null)
        {
            Debug.LogError($"{nameof(PlayerCombatNetworkController)} requires a component implementing {nameof(IAttack)}.", this);
            return false;
        }

        if (_movementController == null)
        {
            Debug.LogError($"{nameof(PlayerCombatNetworkController)} requires an assigned {nameof(PlayerMovementNetworkController)}.", this);
            return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_characterSource == null)
        {
            _characterSource = GetComponent<MonoBehaviour>() as ICharacter != null ? GetComponent<MonoBehaviour>() : null;
        }

        if (_movementController == null)
        {
            _movementController = GetComponent<PlayerMovementNetworkController>();
        }

        if (_activeAttackSource == null)
        {
            IAttack foundAttack = GetComponent<IAttack>() ?? GetComponentInChildren<IAttack>();
            if (foundAttack is MonoBehaviour attackMb)
            {
                _activeAttackSource = attackMb;
            }
        }

        CacheDependencies();
    }
#endif
}
