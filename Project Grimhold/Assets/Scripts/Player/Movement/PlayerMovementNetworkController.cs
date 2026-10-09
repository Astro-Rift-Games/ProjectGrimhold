using Fusion;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

/// <summary>
/// Consumes Fusion input during network ticks and delegates movement
/// resolution to the kinematic movement motor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Kinematic2DMovementMotor))]
[DefaultExecutionOrder(-10)]
public sealed class PlayerMovementNetworkController : NetworkBehaviour, IMovementState, IKnockbackMotor
{
    [SerializeField, Min(0f)]
    private float _moveSpeed = 5f;

    [Header("Sprint Balance (Temporary)")]
    [SerializeField, Min(1f)]
    private float _sprintSpeedMultiplier = 1.5f;

    [SerializeField, Min(0f)]
    private float _sprintStaminaCostPerSecond = 10f;

    [SerializeField]
    private Kinematic2DMovementMotor _movementMotor;

    [SerializeField]
    private PlayerStaminaNetworkController _staminaController;

    [SerializeField]
    private PlayerShieldDefenseNetworkController _shieldDefenseController;

    private bool _dependenciesValid;

    private const float ValidMovementSqrThreshold = 0.000001f;

    [Header("Knockback")]
    [Tooltip("Friction applied to decay knockback velocity over time.")]
    [SerializeField, Min(0f)] private float _knockbackFriction = 10f;

    [SerializeField]
    private Vector2 _defaultFacingDirection = Vector2.down;

    [Networked]
    public NetworkBool IsControlEnabled { get; private set; }

    [Networked]
    public Vector2 FacingDirection { get; private set; }

    /// <summary>
    /// Continuous cursor aim, independent of locomotion facing. Ranged weapons shoot and render along it.
    /// </summary>
    [Networked]
    public Vector2 AimDirection { get; private set; }

    /// <summary>
    /// Accepted aim stance: the secondary action is held with a two-handed aim-stance weapon. Replicated so proxies
    /// present the same aim-driven facing; the facing itself comes from the shared facing rule.
    /// </summary>
    [Networked]
    public NetworkBool IsAimStance { get; private set; }

    /// <summary>
    /// Tick the current aim stance started, or -1 while none is held. It starts on the transition into the stance
    /// and resets on leaving, so every peer derives the same drawn state from ticks.
    /// </summary>
    [Networked]
    public int AimStanceStartTick { get; private set; }

    [Networked]
    public NetworkBool IsMoving { get; private set; }

    /// <summary>
    /// Current knockback velocity. Applied as displacement during ticks and decays via friction.
    /// Only written by State Authority via <see cref="ApplyKnockbackImpulse"/>.
    /// </summary>
    [Networked]
    private Vector2 KnockbackVelocity { get; set; }

    /// <summary>Unit direction of the active forced displacement. Written by State Authority only.</summary>
    [Networked]
    private Vector2 ForcedDirection { get; set; }

    [Networked]
    private float ForcedSpeed { get; set; }

    [Networked]
    private NetworkBool ForcedActive { get; set; }

    /// <summary>
    /// Whether an ability-imposed forced displacement currently moves the avatar. The duration is owned
    /// by the caller, which ends it with <see cref="EndForcedDisplacement"/>.
    /// </summary>
    public bool IsForcedDisplacementActive => ForcedActive;

    /// <summary>Displacement the motor actually applied in the last simulated tick (local, not replicated).</summary>
    public Vector2 LastAppliedDisplacement { get; private set; }

    /// <summary>
    /// True when the last forced step advanced the avatar less than requested along the forced direction,
    /// i.e. Environment blocked it (local, not replicated).
    /// </summary>
    public bool WasForcedDisplacementBlocked { get; private set; }

    private CharacterBase _characterBase;
    private PlayerDownedStateNetworkController _downedState;
    private PlayerWeaponEquipmentNetworkController _equipmentController;
    private PlayerAbilityRuntimeNetworkController _abilityRuntime;
    private NetworkMatchController _matchController;
    private NetworkMatchController.MatchPhase _lastObservedPhase;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _dependenciesValid = ValidateDependencies();
        _matchController = Runner.GetComponent<NetworkMatchController>();
        _lastObservedPhase = _matchController != null
            ? _matchController.Phase
            : NetworkMatchController.MatchPhase.InProgress;

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            IsControlEnabled = _matchController == null ||
                               _matchController.Phase == NetworkMatchController.MatchPhase.InProgress;

            FacingDirection =
                PlayerAimMath.NormalizeInitialFacing(_defaultFacingDirection);
            AimDirection = FacingDirection;
            IsAimStance = false;
            AimStanceStartTick = AimStanceDraw.NoStart;
            IsMoving = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!_dependenciesValid)
        {
            return;
        }

        IsMoving = false;

        bool gameplayPhaseActive = _matchController == null ||
                                    _matchController.Phase == NetworkMatchController.MatchPhase.InProgress;
        if (HasStateAuthority && _matchController != null &&
            _lastObservedPhase != NetworkMatchController.MatchPhase.InProgress &&
            _matchController.Phase == NetworkMatchController.MatchPhase.InProgress)
        {
            IsControlEnabled = true;
        }

        _lastObservedPhase = _matchController != null
            ? _matchController.Phase
            : NetworkMatchController.MatchPhase.InProgress;

        bool hasInput = GetInput(out PlayerNetworkInput input);
        Vector2 moveDirection = hasInput
            ? Vector2.ClampMagnitude(input.MoveDirection, 1f)
            : Vector2.zero;

        bool isAlive = _characterBase == null || _characterBase.IsAlive;
        bool canMove = gameplayPhaseActive && IsControlEnabled && isAlive;

        // Downed is networked state, so reading it here stays resimulation-safe.
        bool isDowned = _downedState != null && _downedState.IsDowned;
        bool shouldSprint = ShouldSprint(in input, hasInput, moveDirection, canMove, isDowned);
        bool isSprinting = shouldSprint && CanSprint(Runner.DeltaTime);
        float effectiveSpeed = ResolveVoluntarySpeed(
            _moveSpeed,
            isSprinting,
            _sprintSpeedMultiplier,
            isDowned,
            isDowned ? _downedState.DownedMovementSpeedMultiplier : 1f);

        if (ForcedActive && HasStateAuthority && Runner.IsForward &&
            !ForcedDisplacementMath.ShouldRemainActive(isAlive, isDowned))
        {
            EndForcedDisplacement();
        }

        // Voluntary and forced movement are different events: a forced displacement replaces voluntary input.
        bool forcedActive = ForcedActive;
        Vector2 forcedStep = forcedActive
            ? ForcedDisplacementMath.ComputeStep(ForcedDirection, ForcedSpeed, Runner.DeltaTime)
            : Vector2.zero;

        Vector2 displacement = canMove && !forcedActive
            ? moveDirection * effectiveSpeed * Runner.DeltaTime
            : Vector2.zero;
        displacement += forcedStep;

        // Apply decaying knockback velocity.
        if (KnockbackVelocity.sqrMagnitude > 0.01f)
        {
            displacement += KnockbackVelocity * Runner.DeltaTime;
            KnockbackVelocity = Vector2.Lerp(KnockbackVelocity, Vector2.zero, _knockbackFriction * Runner.DeltaTime);
        }
        else
        {
            KnockbackVelocity = Vector2.zero;
        }

        Vector2 appliedDisplacement = _movementMotor.Move(displacement);
        LastAppliedDisplacement = appliedDisplacement;
        WasForcedDisplacementBlocked = forcedActive &&
            ForcedDisplacementMath.IsBlocked(forcedStep, appliedDisplacement);

        if (appliedDisplacement.sqrMagnitude > ValidMovementSqrThreshold)
        {
            IsMoving = true;
        }

        bool aimStanceAccepted = hasInput && PlayerAimStanceRules.IsAccepted(
            input.Buttons.IsSet(PlayerInputButton.SecondaryAction),
            WeaponAllowsAimStance(),
            gameplayPhaseActive,
            isAlive,
            isDowned,
            HasActiveShield());
        AimStanceStartTick = AimStanceDraw.NextStartTick(AimStanceStartTick, aimStanceAccepted, (int)Runner.Tick);
        IsAimStance = aimStanceAccepted;

        // Combat consumes FacingDirection later in this simulation tick. Locomotion
        // supplies the default facing, while contextual actions may override it from
        // the cursor using the motor's final position as their canonical origin.
        if (gameplayPhaseActive && hasInput && isAlive)
        {
            bool isDefenseAccepted = _shieldDefenseController != null &&
                _shieldDefenseController.CanDefend(input.Buttons);
            FacingDirection = ResolveFacingDirection(
                in input,
                moveDirection,
                (Vector2)transform.position,
                FacingDirection,
                isDefenseAccepted,
                aimStanceAccepted);
            AimDirection = PlayerAimMath.ResolveAimDirection(
                in input,
                (Vector2)transform.position,
                AimDirection);
        }
    }

    /// <summary>Whether the held aim stance has been drawn for the weapon's draw time as of the given tick.</summary>
    public bool IsAimStanceFullyDrawn(int tick, float drawSeconds) =>
        IsAimStance && AimStanceDraw.IsFullyDrawn(tick, AimStanceStartTick, drawSeconds, Runner.DeltaTime);

    /// <summary>
    /// Seconds the held aim stance has been drawn on the render timeline, for presentation on any peer.
    /// </summary>
    public bool TryGetAimStanceElapsedSeconds(out float seconds)
    {
        seconds = 0f;
        if (Runner == null || !Runner.IsRunning || Object == null || !Object.IsValid || !IsAimStance ||
            AimStanceStartTick < 0)
        {
            return false;
        }

        double renderTime = HasStateAuthority ? Runner.LocalRenderTime : Runner.RemoteRenderTime;
        seconds = Mathf.Max(0f, AttackTiming.ElapsedSeconds(renderTime, AimStanceStartTick, Runner.DeltaTime));
        return true;
    }

    public bool TrySetControlEnabled(bool enabled)
    {
        if (!HasStateAuthority)
        {
            return false;
        }

        IsControlEnabled = enabled;
        return true;
    }

    /// <summary>
    /// Accumulates a knockback impulse to be applied in the current simulation tick.
    /// Requires State Authority. Called by <see cref="CharacterBase"/> after receiving damage.
    ///
    /// The displacement is computed as <c>-impactDirection * force * DeltaTime</c>.
    /// It is additive: multiple simultaneous hits stack within the same tick.
    /// </summary>
    public void ApplyKnockbackImpulse(Vector2 impactDirection, float force)
    {
        if (!HasStateAuthority || force <= 0f)
        {
            return;
        }

        // Add to velocity so it decays over time.
        KnockbackVelocity += impactDirection.normalized * force;

        // A push that applied is a Knockback: interruptible executions stop, the rest decide for themselves.
        if (_abilityRuntime != null)
        {
            _abilityRuntime.InterruptActiveExecutions(AbilityExecutionStopReason.Knockback);
        }
    }

    /// <summary>
    /// Starts a forced displacement along <paramref name="direction"/> at <paramref name="speed"/> until
    /// <see cref="EndForcedDisplacement"/>. State Authority and forward simulation only; rejects an invalid
    /// request or one while another is active. Voluntary input is suppressed while active; knockback still adds.
    /// </summary>
    public bool TryBeginForcedDisplacement(Vector2 direction, float speed)
    {
        if (!HasStateAuthority || Runner == null || !Runner.IsForward || ForcedActive ||
            !ForcedDisplacementMath.TryValidate(direction, speed, out Vector2 normalizedDirection))
        {
            return false;
        }

        ForcedDirection = normalizedDirection;
        ForcedSpeed = speed;
        ForcedActive = true;
        return true;
    }

    /// <summary>Ends the forced displacement. State Authority and forward simulation only; idempotent.</summary>
    public void EndForcedDisplacement()
    {
        if (!HasStateAuthority || Runner == null || !Runner.IsForward)
        {
            return;
        }

        ForcedActive = false;
        ForcedDirection = Vector2.zero;
        ForcedSpeed = 0f;
        WasForcedDisplacementBlocked = false;
    }

    // Attack, Interact and an accepted shield defense aim at the cursor while held. The secondary
    // action alone never does: without a defendable shield it keeps the locomotion facing.
    internal static Vector2 ResolveFacingDirection(
        in PlayerNetworkInput input,
        Vector2 moveDirection,
        Vector2 finalPosition,
        Vector2 previousFacing,
        bool isDefenseAccepted = false,
        bool isAimStanceAccepted = false)
    {
        Vector2 resolvedFacing = previousFacing;
        if (PlayerAimMath.TryNormalizeDirection(moveDirection, out Vector2 movementFacing))
        {
            resolvedFacing = movementFacing;
        }

        bool hasContextualFacingIntent =
            input.Buttons.IsSet(PlayerInputButton.PrimaryAttack) ||
            input.Buttons.IsSet(PlayerInputButton.Interact) ||
            isDefenseAccepted ||
            isAimStanceAccepted;
        if (hasContextualFacingIntent &&
            PlayerAimMath.TryResolveDirection(
                finalPosition,
                input.AimWorldPosition,
                out Vector2 contextualFacing))
        {
            resolvedFacing = contextualFacing;
        }

        return resolvedFacing;
    }

    internal static bool ShouldSprint(
        in PlayerNetworkInput input,
        bool hasInput,
        Vector2 moveDirection,
        bool canMove,
        bool isDowned = false)
    {
        return hasInput && canMove && !isDowned &&
               moveDirection.sqrMagnitude > ValidMovementSqrThreshold &&
               input.Buttons.IsSet(PlayerInputButton.Sprint);
    }

    /// <summary>
    /// Resolves voluntary locomotion speed. Downed players move at a reduced speed and never sprint.
    /// Knockback is composed separately and is unaffected.
    /// </summary>
    internal static float ResolveVoluntarySpeed(
        float moveSpeed,
        bool isSprinting,
        float sprintSpeedMultiplier,
        bool isDowned,
        float downedSpeedMultiplier)
    {
        if (isDowned)
        {
            return moveSpeed * downedSpeedMultiplier;
        }

        return isSprinting ? moveSpeed * sprintSpeedMultiplier : moveSpeed;
    }

    private bool WeaponAllowsAimStance()
    {
        return _equipmentController != null &&
            _equipmentController.TryGetEquippedDefinition(out LootDefinition loot) &&
            PlayerAimStanceRules.WeaponAllows(loot.WeaponDefinition);
    }

    private bool HasActiveShield()
    {
        return _equipmentController != null && _equipmentController.TryGetActiveShieldDefinition(out _);
    }

    private bool CanSprint(float deltaTime)
    {
        return _staminaController != null &&
               _staminaController.TrySpendContinuous(_sprintStaminaCostPerSecond * deltaTime);
    }

    private void CacheDependencies()
    {
        if (_movementMotor == null)
        {
            _movementMotor =
                GetComponent<Kinematic2DMovementMotor>();
        }

        if (_characterBase == null)
        {
            _characterBase = GetComponent<CharacterBase>();
        }

        if (_staminaController == null)
        {
            _staminaController = GetComponent<PlayerStaminaNetworkController>();
        }

        if (_downedState == null)
        {
            _downedState = GetComponent<PlayerDownedStateNetworkController>();
        }

        if (_shieldDefenseController == null)
        {
            _shieldDefenseController = GetComponent<PlayerShieldDefenseNetworkController>();
        }

        if (_equipmentController == null)
        {
            _equipmentController = GetComponent<PlayerWeaponEquipmentNetworkController>();
        }

        if (_abilityRuntime == null)
        {
            _abilityRuntime = GetComponent<PlayerAbilityRuntimeNetworkController>();
        }
    }

    private bool ValidateDependencies()
    {
        if (_movementMotor != null)
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(PlayerMovementNetworkController)} requires " +
            $"{nameof(Kinematic2DMovementMotor)}.",
            this);

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _moveSpeed = Mathf.Max(0f, _moveSpeed);
        _sprintSpeedMultiplier = Mathf.Max(1f, _sprintSpeedMultiplier);
        _sprintStaminaCostPerSecond = Mathf.Max(0f, _sprintStaminaCostPerSecond);

        if (_movementMotor == null)
        {
            _movementMotor =
                GetComponent<Kinematic2DMovementMotor>();
        }
    }
#endif
}

/// <summary>
/// Deterministic, allocation-free rules for the forced displacement hook (fixed direction and speed
/// imposed by an ability). It owns no duration: the caller ends the displacement explicitly.
/// </summary>
internal static class ForcedDisplacementMath
{
    /// <summary>Forward shortfall (world units) below which a forced step counts as fully applied.</summary>
    internal const float BlockedEpsilon = 0.0001f;

    /// <summary>
    /// Validates a request and normalizes its direction. Rejects zero or non-finite directions and
    /// non-positive or non-finite speeds.
    /// </summary>
    internal static bool TryValidate(Vector2 direction, float speed, out Vector2 normalizedDirection)
    {
        normalizedDirection = Vector2.zero;

        if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
        {
            return false;
        }

        return PlayerAimMath.TryNormalizeDirection(direction, out normalizedDirection);
    }

    internal static Vector2 ComputeStep(Vector2 direction, float speed, float deltaTime)
    {
        return direction * (speed * deltaTime);
    }

    /// <summary>
    /// A forced step is blocked when the motor advanced the avatar along the requested direction by less
    /// than requested. Sliding along a surface gives no forward progress, so it counts as blocked.
    /// </summary>
    internal static bool IsBlocked(Vector2 requestedStep, Vector2 appliedDisplacement)
    {
        float requestedLength = requestedStep.magnitude;
        if (requestedLength <= BlockedEpsilon)
        {
            return false;
        }

        float forwardProgress = Vector2.Dot(appliedDisplacement, requestedStep / requestedLength);
        return requestedLength - forwardProgress > BlockedEpsilon;
    }

    internal static bool ShouldRemainActive(bool isAlive, bool isDowned)
    {
        return isAlive && !isDowned;
    }
}

/// <summary>
/// Provides deterministic, allocation-free aim direction calculations for player simulation.
/// </summary>
internal static class PlayerAimMath
{
    internal const float MinimumDirectionSqrMagnitude = 0.0001f;

    /// <summary>
    /// Resolves a normalized direction from a simulated origin to an aim world position.
    /// </summary>
    internal static bool TryResolveDirection(
        Vector2 origin,
        Vector2 aimWorldPosition,
        out Vector2 direction)
    {
        direction = Vector2.zero;

        if (!IsFinite(origin) || !IsFinite(aimWorldPosition))
        {
            return false;
        }

        Vector2 delta = aimWorldPosition - origin;
        return TryNormalizeDirection(delta, out direction);
    }

    /// <summary>
    /// Resolves the continuous aim from the input cursor. A zero cursor is the "no aim" sentinel
    /// (suppressed input or missing camera), so the previous aim is kept.
    /// </summary>
    internal static Vector2 ResolveAimDirection(
        in PlayerNetworkInput input,
        Vector2 finalPosition,
        Vector2 previousAim)
    {
        if (input.AimWorldPosition == Vector2.zero)
        {
            return previousAim;
        }

        return TryResolveDirection(finalPosition, input.AimWorldPosition, out Vector2 aim)
            ? aim
            : previousAim;
    }

    /// <summary>
    /// Chooses the committed attack direction. Ranged attacks follow the continuous aim and fall back to
    /// the locomotion facing when the aim is unrestored or legacy (near zero); melee always uses facing.
    /// </summary>
    internal static bool TryResolveAttackDirection(
        bool isRanged,
        Vector2 facingDirection,
        Vector2 aimDirection,
        out Vector2 direction)
    {
        if (isRanged && TryNormalizeDirection(aimDirection, out direction))
        {
            return true;
        }

        return TryNormalizeDirection(facingDirection, out direction);
    }

    /// <summary>
    /// Validates and normalizes a direction without applying any fallback.
    /// </summary>
    internal static bool TryNormalizeDirection(Vector2 value, out Vector2 direction)
    {
        direction = Vector2.zero;

        if (!IsFinite(value))
        {
            return false;
        }

        float sqrMagnitude = value.sqrMagnitude;
        if (!IsFinite(sqrMagnitude) ||
            sqrMagnitude < MinimumDirectionSqrMagnitude)
        {
            return false;
        }

        Vector2 normalizedDirection = value.normalized;
        if (!IsFinite(normalizedDirection))
        {
            return false;
        }

        direction = normalizedDirection;
        return true;
    }

    /// <summary>
    /// Normalizes configured initial facing and supplies the supported final fallback.
    /// </summary>
    internal static Vector2 NormalizeInitialFacing(Vector2 configuredFacing)
    {
        return TryNormalizeDirection(configuredFacing, out Vector2 normalizedFacing)
            ? normalizedFacing
            : Vector2.down;
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
