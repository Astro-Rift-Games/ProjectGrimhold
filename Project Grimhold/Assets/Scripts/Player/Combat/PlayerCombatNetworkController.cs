using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Network component responsible for processing player attack intentions
/// and delegating execution to the active attack strategy.
///
/// This controller operates with any strategy implementing the
/// <see cref="IAttack"/> contract, or with no strategy while the player is neutral.
/// Authoritative strategy presence is replicated independently from the local implementation,
/// isolating gameplay simulation from visual presentation.
/// </summary>
[DisallowMultipleComponent]
// Movement writes the final-tick FacingDirection before combat consumes it.
[DefaultExecutionOrder(-7)]
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

    [SerializeField]
    private PlayerShieldDefenseNetworkController _shieldDefenseController;

    [Tooltip("Required same-avatar effect when this avatar composes Empower. Optional only on avatars without Empower capability; those use multiplier 1.")]
    [SerializeField]
    private EmpowerNetworkEffect _empowerEffect;

    private ICharacter _character;
    private IAttack _activeAttack;
    private bool _dependenciesValid;
    private NetworkMatchController _matchController;
    private NetworkSpawnManager _spawnManager;
    private PlayerWeaponEquipmentNetworkController _equipmentController;
    private NetworkMatchController.MatchPhase _lastObservedPhase;
    private int _lastObservedSequence;
    private bool _resumePendingPresentation;
    private IProjectileSpawner _releaseSpawner;

    [Networked, Capacity(4)] private NetworkArray<WeaponHandAttackState> HandAttacks => default;
    [Networked] private int WeaponExecutionEndTick { get; set; }
    private readonly int[] _observedHandSequences = new int[4];
    private readonly bool[] _resumeHandPresentation = new bool[4];
    private PlayerStaminaNetworkController _stamina;

    public bool IsExecutingWeaponAttack => Object != null && Object.IsValid && Runner != null &&
        Runner.Tick < WeaponExecutionEndTick;

    [Networked] private RangedAttackRelease PendingRangedRelease { get; set; }
    [Networked] private MeleeAttackRelease PendingMeleeRelease { get; set; }
    [Networked] private int LastAttackReleaseTick { get; set; }
    [Networked] private int LastAttackCancellationTick { get; set; }
    private readonly Queue<CombatPresentationEvent> _pendingFeedbackEvents = new();

    [Networked]
    private NetworkButtons PreviousButtons { get; set; }

    [Networked]
    private TickTimer AttackCooldown { get; set; }

    [Networked]
    private NetworkBool HasActiveAttack { get; set; }

    [Networked]
    private float AttackCooldownDurationSeconds { get; set; }

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
    private NetworkBool LastAttackAimed { get; set; }

    [Networked]
    private int LastAttackWeaponCatalogIndexPlusOne { get; set; }

    [Networked]
    private int CombatFeedbackSequence { get; set; }

    /// <summary>
    /// Local event raised during Render for a confirmed attack acceptance (melee or ranged), before its release tick.
    /// </summary>
    public event Action<AttackPerformedEvent> AttackPerformed;

    // Reconstruct an in-flight wind-up on spawn/migration without replaying attack-start audio.
    public event Action<AttackPerformedEvent> AttackPresentationResumed;

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
        _spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        _matchController = _spawnManager != null ? _spawnManager.MatchController : Runner.GetComponent<NetworkMatchController>();
        _lastObservedPhase = _matchController != null
            ? _matchController.Phase
            : NetworkMatchController.MatchPhase.InProgress;

        // Initialize the local observed sequence with the current network sequence
        // to prevent triggering events from attacks performed before this proxy spawned.
        _lastObservedSequence = AttackSequence;
        _resumePendingPresentation = PendingRangedRelease.Pending || PendingMeleeRelease.Pending;
        _pendingFeedbackEvents.Clear();
        for (int i = 0; i < 4; i++)
        {
            _observedHandSequences[i] = HandAttacks[i].Sequence;
            _resumeHandPresentation[i] = HandAttacks[i].HasPendingRelease;
        }

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            for (int i = 0; i < 4; i++) HandAttacks.Set(i, default);
            WeaponExecutionEndTick = 0;
            HasActiveAttack = _activeAttack != null;
            AttackCooldown = TickTimer.None;
            AttackCooldownDurationSeconds = 0f;
            PendingRangedRelease = default;
            PendingMeleeRelease = default;
            LastAttackReleaseTick = -1;
            LastAttackCancellationTick = -1;
            LastAttackAimed = false;
            IsAttackEnabled = _matchController == null ||
                              _matchController.Phase == NetworkMatchController.MatchPhase.InProgress;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!_dependenciesValid)
        {
            return;
        }

        // Restore order may spawn the avatar before the match object; resolve again before ticking.
        _matchController ??= _spawnManager != null ? _spawnManager.MatchController : Runner.GetComponent<NetworkMatchController>();
        bool gameplayPhaseActive = _matchController == null ||
                                    _matchController.Phase == NetworkMatchController.MatchPhase.InProgress;
        if (HasStateAuthority && _matchController != null &&
            _lastObservedPhase != NetworkMatchController.MatchPhase.InProgress &&
            _matchController.Phase == NetworkMatchController.MatchPhase.InProgress)
        {
            IsAttackEnabled = true;
        }

        _lastObservedPhase = _matchController != null
            ? _matchController.Phase
            : NetworkMatchController.MatchPhase.InProgress;

        // Pending work is authority-only, forward-only, and independent of input/current equipment.
        if (HasStateAuthority && Runner.IsForward)
        {
            if (!gameplayPhaseActive || !IsAttackEnabled || !_character.IsAlive || PlayerDownedGate.IsDowned(_character))
            {
                CancelPendingRelease();
                AdvanceHandReleases(true);
                WeaponExecutionEndTick = 0;
            }
            else
            {
                AdvancePendingRelease();
                AdvanceHandReleases(false);
            }
        }

        // Input absence must not stall an already accepted release.
        if (!GetInput(out PlayerNetworkInput input))
        {
            return;
        }

        if (_equipmentController != null && _equipmentController.ActiveWeaponSetSlot != WeaponSetSlot.None)
        {
            NetworkButtons previous = PreviousButtons;
            PreviousButtons = input.Buttons;
            if (HasStateAuthority && Runner.IsForward && gameplayPhaseActive)
                ProcessHandInput(input.Buttons, previous);
            return;
        }

        NetworkButtons currentButtons = input.Buttons;
        bool defenseRequested = _shieldDefenseController != null &&
            _shieldDefenseController.CanDefend(currentButtons);
        bool attackPressedThisTick = currentButtons.WasPressed(
            PreviousButtons,
            PlayerInputButton.PrimaryAttack);
        bool attackPressed = false;

        if (!defenseRequested && HasActiveAttack && _activeAttack != null)
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
        if (!HasStateAuthority || !Runner.IsForward)
        {
            return;
        }

        if (!gameplayPhaseActive || !HasActiveAttack || _activeAttack == null || !attackPressed)
        {
            return;
        }

        PlayerReviveGate.InterruptIfReviving(_character);
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

        RenderHandAttacks();
        // Use the projectile timeframe even on an Input-Authority client (player movement is predicted,
        // projectiles are not). Do not present an acceptance ahead of that render timeline.
        AttackPerformedEvent performedEvent = GetLastAttackEvent();
        if ((AttackSequence != _lastObservedSequence || _resumePendingPresentation) &&
            (!performedEvent.HasReleaseTimeline || GetAttackElapsedSeconds(performedEvent) >= 0f))
        {
            bool resume = _resumePendingPresentation && AttackSequence == _lastObservedSequence;
            _resumePendingPresentation = false;
            _lastObservedSequence = AttackSequence;
            if (!performedEvent.HasReleaseTimeline || TryGetAttackPresentationSeconds(performedEvent, out _))
            {
                if (resume) AttackPresentationResumed?.Invoke(performedEvent);
                else AttackPerformed?.Invoke(performedEvent);
            }
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
        if (_equipmentController != null && _equipmentController.ActiveWeaponSetSlot != WeaponSetSlot.None)
            return TryGetAttackStatus(_equipmentController.GetActiveHandSlot(false), out status);
        status = default;
        if (Runner == null || !Runner.IsRunning || !_dependenciesValid ||
            _character == null || !HasActiveAttack)
        {
            return false;
        }

        float remainingSeconds = AttackCooldown.RemainingTime(Runner) ?? 0f;
        status = new PrimaryAttackStatus(
            GetPrimaryAttackFailureReason() == AttackFailureReason.None,
            AttackCooldownDurationSeconds,
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
        if (!HasActiveAttack || _activeAttack == null || _movementController == null)
        {
            return;
        }

        Vector2 originPos = GetAttackOriginPosition();

        // Ranged shots follow the shared continuous aim; melee keeps the locomotion facing.
        if (!PlayerAimMath.TryResolveAttackDirection(
                _activeAttack is RangedAttack,
                _movementController.FacingDirection,
                _movementController.AimDirection,
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

        IAttack executedAttack = _activeAttack;
        bool accepted;
        int releaseTick = -1;
        bool aimedRelease = false;
        if (executedAttack is RangedAttack rangedAttack)
        {
            RangedAttackRelease release = default;
            // A fully drawn aim stance releases after the weapon's short aimed delay; otherwise the normal one.
            float? aimedSeconds = TryResolveAimedReleaseSeconds(request.SimulationTick, out float seconds)
                ? seconds
                : null;
            aimedRelease = aimedSeconds.HasValue;
            accepted = _releaseSpawner != null &&
                rangedAttack.TryAcceptRelease(request, Runner.DeltaTime, out release, aimedSeconds,
                    GetBasicAttackDamageMultiplier());
            if (!accepted) return;
            // Capture all projectile data before the shared executor can be reconfigured.
            PendingRangedRelease = release;
            releaseTick = release.ReleaseTick;
        }
        else if (executedAttack is MeleeAttack meleeAttack)
        {
            // The swing resolves damage on its release tick; origin is sampled then, not at acceptance.
            accepted = meleeAttack.TryAcceptRelease(
                request,
                GetActiveWeaponCatalogIndexPlusOne(),
                Runner.DeltaTime,
                out MeleeAttackRelease meleeRelease);
            if (!accepted) return;
            PendingMeleeRelease = meleeRelease;
            releaseTick = meleeRelease.ReleaseTick;
        }
        else
        {
            accepted = executedAttack.Execute(in request).WasExecuted;
        }

        if (accepted)
        {
            float cooldownSeconds = executedAttack.CooldownSeconds;
            AttackCooldownDurationSeconds = cooldownSeconds;
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
            LastAttackTypeValue = (int)executedAttack.Type;
            LastAttackTick = request.SimulationTick;
            LastAttackReleaseTick = releaseTick;
            LastAttackCancellationTick = -1;
            LastAttackAimed = aimedRelease;
            LastAttackWeaponCatalogIndexPlusOne = GetActiveWeaponCatalogIndexPlusOne();

            // Increment sequence last to ensure correct replication of all related fields
            AttackSequence++;
            // Zero-delay configurations retain same-tick release, still through consume-before-spawn.
            AdvancePendingRelease();
        }
    }

    private bool TryResolveAimedReleaseSeconds(int tick, out float seconds)
    {
        seconds = 0f;
        if (_equipmentController == null || !_equipmentController.TryGetEquippedDefinition(out LootDefinition loot))
        {
            return false;
        }

        WeaponDefinition weapon = loot.WeaponDefinition;
        bool fullyDrawn = weapon != null &&
            _movementController.IsAimStanceFullyDrawn(tick, weapon.AimStanceDrawSeconds);
        return AimStanceDraw.TrySelectAimedReleaseSeconds(weapon, fullyDrawn, out seconds);
    }

    private Vector2 GetAttackOriginPosition() => _attackOrigin != null
        ? (Vector2)_attackOrigin.position
        : (Vector2)transform.position;

    // Applies to the player's basic weapon attacks only: enemies and abilities never go through this controller.
    private float GetBasicAttackDamageMultiplier() => _empowerEffect != null
        ? _empowerEffect.GetBasicAttackDamageMultiplier()
        : 1f;

    private int GetActiveWeaponCatalogIndexPlusOne() => _equipmentController != null
        ? _equipmentController.GetActiveWeaponCatalogIndexPlusOne()
        : 0;

    private void AdvancePendingRelease()
    {
        AdvancePendingMeleeRelease();
        AdvancePendingRangedRelease();
    }

    private void AdvancePendingMeleeRelease()
    {
        MeleeAttackRelease release = PendingMeleeRelease;
        if (!release.Pending) return;

        // A swing belongs to the weapon that started it; swapping weapons discards it immediately.
        if (release.CancelIfWeaponChanged(GetActiveWeaponCatalogIndexPlusOne()))
        {
            PendingMeleeRelease = release;
            LastAttackCancellationTick = Runner.Tick;
            return;
        }

        if (Runner.Tick < release.ReleaseTick) return;

        bool valid = release.TryConsume(
            Runner.Tick,
            GetActiveWeaponCatalogIndexPlusOne(),
            out Vector2 direction);
        PendingMeleeRelease = release; // Commit consumption BEFORE the irreversible damage, even on failure.
        LastAttackCancellationTick = Runner.Tick;
        if (valid && _activeAttack is MeleeAttack meleeAttack)
        {
            AttackRequest request = new AttackRequest(
                _character.Id,
                GetAttackOriginPosition(),
                direction,
                (int)Runner.Tick);
            if (meleeAttack.Execute(in request, GetBasicAttackDamageMultiplier()).WasExecuted)
                LastAttackCancellationTick = -1;
        }
    }

    private void AdvancePendingRangedRelease()
    {
        RangedAttackRelease release = PendingRangedRelease;
        if (!release.Pending || Runner.Tick < release.ReleaseTick) return;
        bool valid = release.TryConsume(Runner.Tick, _character.Id, _attackOrigin.position, out ProjectileSpawnRequest request);
        PendingRangedRelease = release; // Commit consumption BEFORE the irreversible spawn, even on failure.
        // A throwing adapter must also leave a consumed/cancelled shot; do not hide its exception.
        LastAttackCancellationTick = Runner.Tick;
        if (valid && _releaseSpawner != null && _releaseSpawner.Spawn(request).WasSpawned)
            LastAttackCancellationTick = -1;
    }

    private void CancelPendingRelease()
    {
        RangedAttackRelease release = PendingRangedRelease;
        if (release.Pending)
        {
            release.Cancel();
            PendingRangedRelease = release;
            LastAttackCancellationTick = Runner.Tick;
        }

        MeleeAttackRelease meleeRelease = PendingMeleeRelease;
        if (meleeRelease.Pending)
        {
            meleeRelease.Cancel();
            PendingMeleeRelease = meleeRelease;
            LastAttackCancellationTick = Runner.Tick;
        }
        // AttackCooldown and its duration are deliberately untouched.
    }

    private AttackPerformedEvent GetLastAttackEvent() => new AttackPerformedEvent(
        _character.Id, (AttackType)LastAttackTypeValue, LastAttackOrigin, LastAttackDirection,
        LastAttackTick, LastAttackWeaponCatalogIndexPlusOne, AttackSequence, LastAttackReleaseTick,
        LastAttackReleaseTick >= LastAttackTick ? (LastAttackReleaseTick - LastAttackTick) * Runner.DeltaTime : 0f,
        LastAttackAimed);

    private double AttackRenderTime => HasStateAuthority ? Runner.LocalRenderTime : Runner.RemoteRenderTime;

    private float GetAttackElapsedSeconds(in AttackPerformedEvent attack) =>
        AttackTiming.ElapsedSeconds(AttackRenderTime, attack.SimulationTick, Runner.DeltaTime);

    /// <summary>One confirmed ranged clock shared by animation/stringing/VFX, never receipt-relative.</summary>
    public bool TryGetAttackPresentationSeconds(in AttackPerformedEvent attack, out float seconds)
    {
        seconds = 0f;
        if (EquipmentSlotRules.IsHandSlot(attack.SourceSlot))
        {
            if (Runner == null || !Runner.IsRunning || Object == null || !Object.IsValid ||
                _character == null || !_character.IsAlive || PlayerDownedGate.IsDowned(_character)) return false;
            int index = HandIndex(attack.SourceSlot);
            WeaponHandAttackState state = HandAttacks[index];
            if (attack.Sequence != _observedHandSequences[index]) return false;
            seconds = GetAttackElapsedSeconds(attack);
            return seconds >= 0f && (state.Sequence != attack.Sequence || state.CancellationTick < 0 ||
                AttackRenderTime < (double)state.CancellationTick * Runner.DeltaTime);
        }
        if (Runner == null || !Runner.IsRunning || Object == null || !Object.IsValid ||
            attack.Sequence != _lastObservedSequence || !_character.IsAlive || PlayerDownedGate.IsDowned(_character))
            return false;
        seconds = GetAttackElapsedSeconds(attack);
        // A newer received snapshot may still be ahead of the remote render clock. It must not
        // prematurely replace the currently observed attack or apply another sequence's cancellation.
        return seconds >= 0f && (attack.Sequence != AttackSequence || LastAttackCancellationTick < 0 ||
            AttackRenderTime < (double)LastAttackCancellationTick * Runner.DeltaTime);
    }

    private AttackFailureReason GetPrimaryAttackFailureReason()
    {
        if (Runner == null || !Runner.IsRunning || !_dependenciesValid ||
            _character == null)
        {
            return AttackFailureReason.MissingConfiguration;
        }

        if (!IsAttackEnabled || !_character.IsAlive || PlayerDownedGate.IsDowned(_character))
        {
            return AttackFailureReason.ControlDisabled;
        }

        if (PendingRangedRelease.Pending || PendingMeleeRelease.Pending) return AttackFailureReason.CooldownActive;

        return AttackCooldown.ExpiredOrNotRunning(Runner)
            ? AttackFailureReason.None
            : AttackFailureReason.CooldownActive;
    }

    private static int HandIndex(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.WeaponSetAMainHand => 0, EquipmentSlot.WeaponSetAOffHand => 1,
        EquipmentSlot.WeaponSetBMainHand => 2, EquipmentSlot.WeaponSetBOffHand => 3,
        _ => -1
    };

    private static EquipmentSlot HandSlot(int index) => index switch
    {
        0 => EquipmentSlot.WeaponSetAMainHand, 1 => EquipmentSlot.WeaponSetAOffHand,
        2 => EquipmentSlot.WeaponSetBMainHand, _ => EquipmentSlot.WeaponSetBOffHand
    };

    public bool TryGetAttackStatus(EquipmentSlot slot, out PrimaryAttackStatus status)
    {
        status = default;
        int index = HandIndex(slot);
        if (index < 0 || Runner == null || !Runner.IsRunning || Object == null || !Object.IsValid ||
            !_dependenciesValid || _equipmentController == null ||
            _equipmentController.GetWeaponCatalogIndexPlusOne(slot) == 0) return false;
        WeaponHandAttackState state = HandAttacks[index];
        bool available = IsAttackEnabled && _character.IsAlive && !PlayerDownedGate.IsDowned(_character) &&
            EquipmentSlotRules.GetWeaponSet(slot) == _equipmentController.ActiveWeaponSetSlot &&
            !IsExecutingWeaponAttack && state.Timing.IsReady(Runner.Tick) && !state.HasPendingRelease;
        if (available && _equipmentController.TryGetSlotDefinition(slot, out LootDefinition loot) &&
            loot.WeaponDefinition.StaminaCost > 0f)
            available = _stamina != null && _stamina.CanSpend(loot.WeaponDefinition.StaminaCost);
        status = new PrimaryAttackStatus(available, state.Timing.IntervalSeconds,
            Mathf.Max(0f, (state.Timing.IntervalEndTick - Runner.Tick) * Runner.DeltaTime));
        return true;
    }

    private void ProcessHandInput(NetworkButtons buttons, NetworkButtons previous)
    {
        if (_shieldDefenseController != null && _shieldDefenseController.CanDefend(buttons)) return;
        // Stable Main-first arbitration. Never accept two starts in one tick, including a zero-length window.
        if (TryStartHand(_equipmentController.GetActiveHandSlot(false), PlayerInputButton.PrimaryAttack, buttons, previous)) return;
        TryStartHand(_equipmentController.GetActiveHandSlot(true), PlayerInputButton.SecondaryAction, buttons, previous);
    }

    private bool TryStartHand(EquipmentSlot slot, PlayerInputButton button, NetworkButtons buttons, NetworkButtons previous)
    {
        int index = HandIndex(slot);
        if (index < 0 || !buttons.IsSet(button) ||
            !_equipmentController.TryPrepareAttack(slot, out IAttack attack, out WeaponDefinition weapon)) return false;
        bool pressed = buttons.WasPressed(previous, button);
        if (attack.InputMode == AttackInputMode.Press && !pressed) return false;
        if (!TryGetAttackStatus(slot, out PrimaryAttackStatus status) || !status.IsAvailable)
        {
            if (pressed) RecordCombatFeedback(CombatFeedbackKind.AttackRejected, default, default, 0f,
                Runner.Tick, AttackFailureReason.CooldownActive);
            return false;
        }
        // A paid weapon cannot bypass an absent/uninitialized Stamina owner.
        if (weapon.StaminaCost > 0f && (_stamina == null || !_stamina.CanSpend(weapon.StaminaCost))) return false;
        if (!PlayerAimMath.TryResolveAttackDirection(attack is RangedAttack, _movementController.FacingDirection,
            _movementController.AimDirection, out Vector2 direction)) return false;
        WeaponHandAttackState state = HandAttacks[index];
        if (!state.Timing.TryStart(Runner.Tick, Runner.DeltaTime, attack.CooldownSeconds,
            weapon.ExecutionWindowSeconds, out int windowEnd)) return false;
        AttackRequest request = new(_character.Id, GetAttackOriginPosition(), direction, Runner.Tick);
        state.ReleaseTick = -1;
        state.Aimed = false;
        int identity = _equipmentController.GetWeaponCatalogIndexPlusOne(slot);
        if (attack is MeleeAttack melee)
        {
            if (!melee.TryAcceptRelease(request, identity, Runner.DeltaTime, out state.Melee)) return false;
            state.ReleaseTick = state.Melee.ReleaseTick;
        }
        else if (attack is RangedAttack ranged)
        {
            float? aimed = !EquipmentSlotRules.IsOffHandSlot(slot) &&
                TryResolveAimedReleaseSeconds(Runner.Tick, out float seconds) ? seconds : null;
            if (_releaseSpawner == null || !ranged.TryAcceptRelease(request, Runner.DeltaTime,
                out state.Ranged, aimed, GetBasicAttackDamageMultiplier())) return false;
            state.ReleaseTick = state.Ranged.ReleaseTick;
            state.Aimed = aimed.HasValue;
        }
        else return false;
        // Acceptance above has no external effects. Commit payment once before publishing any release.
        if (weapon.StaminaCost > 0f && !_stamina.TrySpend(weapon.StaminaCost)) return false;
        PlayerReviveGate.InterruptIfReviving(_character);
        WeaponExecutionEndTick = windowEnd;
        state.Sequence = ++AttackSequence;
        state.StartTick = Runner.Tick;
        state.CancellationTick = -1;
        state.WeaponCatalogIndexPlusOne = identity;
        state.TypeValue = (int)attack.Type;
        state.Origin = request.Origin;
        state.Direction = direction;
        HandAttacks.Set(index, state);
        // Compatibility readback for existing Main-hand consumers, never the source of equipped-hand timers.
        if (!EquipmentSlotRules.IsOffHandSlot(slot))
        {
            AttackCooldown = attack.CooldownSeconds > 0f ? TickTimer.CreateFromSeconds(Runner, attack.CooldownSeconds) : TickTimer.None;
            AttackCooldownDurationSeconds = attack.CooldownSeconds;
        }
        AdvanceHandReleases(false);
        return true;
    }

    private void AdvanceHandReleases(bool cancel)
    {
        if (Runner == null) return;
        for (int index = 0; index < 4; index++)
        {
            WeaponHandAttackState state = HandAttacks[index];
            if (!state.HasPendingRelease) continue;
            if (cancel)
            {
                state.Melee.Cancel();
                state.Ranged.Cancel();
                state.CancellationTick = Runner.Tick;
                HandAttacks.Set(index, state);
                continue;
            }
            EquipmentSlot slot = HandSlot(index);
            if (state.Melee.Pending)
            {
                int identity = _equipmentController != null &&
                    EquipmentSlotRules.GetWeaponSet(slot) == _equipmentController.ActiveWeaponSetSlot
                    ? _equipmentController.GetWeaponCatalogIndexPlusOne(slot) : 0;
                if (state.Melee.CancelIfWeaponChanged(identity))
                {
                    state.CancellationTick = Runner.Tick;
                    HandAttacks.Set(index, state);
                }
                else if (Runner.Tick >= state.Melee.ReleaseTick)
                {
                    MeleeAttackRelease release = state.Melee;
                    bool valid = state.Melee.TryConsume(Runner.Tick, identity, out Vector2 direction);
                    state.CancellationTick = Runner.Tick;
                    HandAttacks.Set(index, state); // Consume BEFORE irreversible damage, also on failure/exception.
                    if (valid && _equipmentController.TryPrepareAttack(slot, out IAttack strategy, out _) && strategy is MeleeAttack melee)
                    {
                        AttackRequest request = new(_character.Id, GetAttackOriginPosition(), direction, Runner.Tick);
                        AttackExecutionParameters parameters = release.ExecutionParameters;
                        if (melee.Execute(request, parameters, GetBasicAttackDamageMultiplier()).WasExecuted)
                        {
                            state.CancellationTick = -1;
                            HandAttacks.Set(index, state);
                        }
                    }
                }
            }
            if (state.Ranged.Pending && Runner.Tick >= state.Ranged.ReleaseTick)
            {
                bool valid = state.Ranged.TryConsume(Runner.Tick, _character.Id, GetAttackOriginPosition(), out ProjectileSpawnRequest request);
                state.CancellationTick = Runner.Tick;
                HandAttacks.Set(index, state);
                if (valid && _releaseSpawner != null && _releaseSpawner.Spawn(request).WasSpawned)
                {
                    state.CancellationTick = -1;
                    HandAttacks.Set(index, state);
                }
            }
        }
    }

    private void RenderHandAttacks()
    {
        // Snapshots from different sets can arrive together. Dispatch by acceptance sequence, not slot,
        // so every consumer finishes on the newest timeline for its hand. Four slots need no allocations.
        for (int delivered = 0; delivered < 4; delivered++)
        {
            int next = -1;
            int nextSequence = int.MaxValue;
            for (int index = 0; index < 4; index++)
            {
                WeaponHandAttackState candidate = HandAttacks[index];
                if (candidate.Sequence == 0 ||
                    (candidate.Sequence == _observedHandSequences[index] && !_resumeHandPresentation[index])) continue;
                AttackPerformedEvent notification = candidate.ToEvent(_character.Id, HandSlot(index), Runner.DeltaTime);
                if (GetAttackElapsedSeconds(notification) < 0f) continue;
                if (next < 0 || candidate.Sequence < nextSequence)
                {
                    next = index;
                    nextSequence = candidate.Sequence;
                }
            }
            if (next < 0) break;
            WeaponHandAttackState state = HandAttacks[next];
            AttackPerformedEvent attack = state.ToEvent(_character.Id, HandSlot(next), Runner.DeltaTime);
            bool resume = _resumeHandPresentation[next] && state.Sequence == _observedHandSequences[next];
            _observedHandSequences[next] = state.Sequence;
            _resumeHandPresentation[next] = false;
            if (!TryGetAttackPresentationSeconds(attack, out _)) continue;
            if (resume) AttackPresentationResumed?.Invoke(attack);
            else AttackPerformed?.Invoke(attack);
        }
        // Equipment attacks use their independent snapshots, not the legacy single-strategy event.
        for (int index = 0; index < 4; index++)
            if (HandAttacks[index].Sequence > 0 && HandAttacks[index].Sequence == AttackSequence)
                _lastObservedSequence = AttackSequence;
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
        if (!enabled)
        {
            CancelPendingRelease();
            AdvanceHandReleases(true);
            WeaponExecutionEndTick = 0;
        }
        return true;
    }

    /// <summary>
    /// Authoritatively assigns a validated active attack strategy without changing
    /// an already running cooldown. Requires State Authority.
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
            HasActiveAttack = true;
            return true;
        }

        Debug.LogError($"{nameof(PlayerCombatNetworkController)}: The component {attackSource.name} does not implement {nameof(IAttack)}.", this);
        return false;
    }

    /// <summary>
    /// Authoritatively removes the active attack strategy without cancelling an
    /// already running cooldown. Requires State Authority.
    /// </summary>
    public bool TryClearActiveAttack()
    {
        if (!HasStateAuthority)
        {
            return false;
        }

        _activeAttackSource = null;
        _activeAttack = null;
        HasActiveAttack = false;
        return true;
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

        _activeAttack = _activeAttackSource as IAttack;
        _equipmentController ??= GetComponent<PlayerWeaponEquipmentNetworkController>();
        _releaseSpawner ??= GetComponent<IProjectileSpawner>();
        _stamina ??= GetComponent<PlayerStaminaNetworkController>();

        if (_attackOrigin == null)
        {
            _attackOrigin = transform;
        }

        if (_movementController == null)
        {
            _movementController = GetComponent<PlayerMovementNetworkController>();
        }

        if (_shieldDefenseController == null)
        {
            _shieldDefenseController = GetComponent<PlayerShieldDefenseNetworkController>();
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

        if (_movementController == null)
        {
            Debug.LogError($"{nameof(PlayerCombatNetworkController)} requires an assigned {nameof(PlayerMovementNetworkController)}.", this);
            return false;
        }

        // This is a one-time composition check, never a gameplay search or an implicit assignment.
        // Generic combat avatars/fixtures deliberately without Empower remain valid with multiplier 1.
        if (_empowerEffect == null &&
            (GetComponent<EmpowerNetworkEffect>() != null || GetComponent<EmpowerAbilityBehaviour>() != null))
        {
            Debug.LogError($"{nameof(PlayerCombatNetworkController)} requires an assigned same-avatar {nameof(EmpowerNetworkEffect)} on an Empower-capable avatar.", this);
            return false;
        }

        if (_empowerEffect != null &&
            (_empowerEffect.gameObject != gameObject || !_empowerEffect.IsOwnerConfigured))
        {
            Debug.LogError($"{nameof(PlayerCombatNetworkController)} requires its assigned {nameof(EmpowerNetworkEffect)} to have a valid same-avatar owner.", this);
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

        CacheDependencies();
    }
#endif
}
