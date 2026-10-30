#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Fusion;
using UnityEngine;

/// <summary>
/// Minimal knockback receiver for the training dummy. The dummy runs no enemy AI, so
/// this decays an impulse and moves the body through the shared kinematic motor
/// (world collisions respected). Position replicates through NetworkTransform.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Kinematic2DMovementMotor))]
public sealed class DummyKnockbackMotor : NetworkBehaviour, IKnockbackMotor
{
    private const float StopSqrSpeed = 0.01f;

    [SerializeField] private Kinematic2DMovementMotor _movementMotor;
    [SerializeField, Min(0f)] private float _friction = 10f;

    // State Authority only; intentionally not networked (remote peers get the resulting position).
    private Vector2 _velocity;

    private float _activeEffectivePower;

    /// <summary>Applies strict replacement while retaining the sandbox's simple decaying movement.</summary>
    public KnockbackDecision TryApplyKnockback(Vector2 direction, float effectivePower, bool wasEligibleAtImpact)
    {
        if (!HasStateAuthority)
        {
            return KnockbackDecision.KeptActive;
        }
        if (!PlayerAimMath.TryNormalizeDirection(direction, out Vector2 normalizedDirection))
        {
            return KnockbackDecision.Absorbed;
        }
        KnockbackDecision decision = KnockbackRules.Resolve(wasEligibleAtImpact, effectivePower,
            _velocity.sqrMagnitude > StopSqrSpeed, _activeEffectivePower);
        if (decision == KnockbackDecision.Started || decision == KnockbackDecision.Replaced)
        {
            _activeEffectivePower = effectivePower;
            _velocity = normalizedDirection * effectivePower;
        }
        return decision;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _velocity.sqrMagnitude <= StopSqrSpeed)
        {
            _velocity = Vector2.zero;
            return;
        }

        if (_movementMotor == null)
        {
            Debug.LogError($"{nameof(DummyKnockbackMotor)} requires a {nameof(Kinematic2DMovementMotor)}.", this);
            _velocity = Vector2.zero;
            return;
        }

        _movementMotor.Move(_velocity * Runner.DeltaTime);
        _velocity = Vector2.Lerp(_velocity, Vector2.zero, _friction * Runner.DeltaTime);
    }
}
#endif
