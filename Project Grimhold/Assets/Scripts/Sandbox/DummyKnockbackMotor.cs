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

    public void ApplyKnockbackImpulse(Vector2 impactDirection, float force)
    {
        if (!HasStateAuthority || force <= 0f || impactDirection.sqrMagnitude <= 0f)
        {
            return;
        }

        _velocity += impactDirection.normalized * force;
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
