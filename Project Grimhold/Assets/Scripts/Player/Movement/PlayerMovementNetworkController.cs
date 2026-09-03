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

    [Networked]
    public NetworkBool IsMoving { get; private set; }

    /// <summary>
    /// Current knockback velocity. Applied as displacement during ticks and decays via friction.
    /// Only written by State Authority via <see cref="ApplyKnockbackImpulse"/>.
    /// </summary>
    [Networked]
    private Vector2 KnockbackVelocity { get; set; }

    private CharacterBase _characterBase;
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

        bool shouldSprint = ShouldSprint(in input, hasInput, moveDirection, canMove);
        float effectiveSpeed = shouldSprint && CanSprint(Runner.DeltaTime)
            ? _moveSpeed * _sprintSpeedMultiplier
            : _moveSpeed;

        Vector2 displacement = canMove
            ? moveDirection * effectiveSpeed * Runner.DeltaTime
            : Vector2.zero;

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

        if (appliedDisplacement.sqrMagnitude > ValidMovementSqrThreshold)
        {
            IsMoving = true;
        }

        // Combat consumes FacingDirection later in this simulation tick. Resolve from
        // the motor's final position so aiming follows the same authoritative state.
        if (gameplayPhaseActive && hasInput && !IsDefaultInput(in input) && isAlive &&
            PlayerAimMath.TryResolveDirection(
                (Vector2)transform.position,
                input.AimWorldPosition,
                out Vector2 aimDirection))
        {
            FacingDirection = aimDirection;
        }
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
    }

    private static bool IsDefaultInput(in PlayerNetworkInput input)
    {
        return input.MoveDirection == Vector2.zero &&
               input.AimWorldPosition == Vector2.zero &&
               input.Buttons.Bits == 0;
    }

    internal static bool ShouldSprint(
        in PlayerNetworkInput input,
        bool hasInput,
        Vector2 moveDirection,
        bool canMove)
    {
        return hasInput && canMove &&
               moveDirection.sqrMagnitude > ValidMovementSqrThreshold &&
               input.Buttons.IsSet(PlayerInputButton.Sprint);
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
