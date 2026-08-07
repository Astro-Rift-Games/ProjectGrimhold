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
public sealed class PlayerMovementNetworkController : NetworkBehaviour, IMovementState
{
    [SerializeField, Min(0f)]
    private float _moveSpeed = 5f;

    [SerializeField]
    private Kinematic2DMovementMotor _movementMotor;

    private bool _dependenciesValid;

    private const float ValidMovementSqrThreshold = 0.000001f;

    [SerializeField]
    private Vector2 _defaultFacingDirection = Vector2.down;

    [Networked]
    public NetworkBool IsControlEnabled { get; private set; }

    [Networked]
    public Vector2 FacingDirection { get; private set; }

    [Networked]
    public NetworkBool IsMoving { get; private set; }

    private CharacterBase _characterBase;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _dependenciesValid = ValidateDependencies();

        if (HasStateAuthority && !Object.IsResume)
        {
            IsControlEnabled = true;

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

        bool hasInput = GetInput(out PlayerNetworkInput input);
        Vector2 moveDirection = hasInput
            ? Vector2.ClampMagnitude(input.MoveDirection, 1f)
            : Vector2.zero;

        bool isAlive = _characterBase == null || _characterBase.IsAlive;
        bool canMove = IsControlEnabled && isAlive;

        Vector2 displacement = canMove
            ? moveDirection * _moveSpeed * Runner.DeltaTime
            : Vector2.zero;

        Vector2 appliedDisplacement = _movementMotor.Move(displacement);

        if (appliedDisplacement.sqrMagnitude > ValidMovementSqrThreshold)
        {
            IsMoving = true;
        }

        // Combat consumes FacingDirection later in this simulation tick. Resolve from
        // the motor's final position so aiming follows the same authoritative state.
        if (hasInput && !IsDefaultInput(in input) && isAlive &&
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

    private static bool IsDefaultInput(in PlayerNetworkInput input)
    {
        return input.MoveDirection == Vector2.zero &&
               input.AimWorldPosition == Vector2.zero &&
               input.Buttons.Bits == 0;
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
