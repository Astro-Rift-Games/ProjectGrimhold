using UnityEngine;

/// <summary>
/// Applies an authoritative knockback displacement to a character's movement motor.
///
/// Implemented by movement controllers that participate in the network simulation
/// (<see cref="PlayerMovementNetworkController"/> and <see cref="EnemyMovementAIController"/>).
///
/// <para>
/// This interface is the boundary between the damage resolution layer and the movement
/// simulation layer. Implementations must be safe to call during <c>FixedUpdateNetwork</c>
/// on the State Authority.
/// </para>
/// </summary>
public interface IKnockbackMotor
{
    /// <summary>
    /// Accumulates a knockback impulse to be applied during the current or next
    /// simulation tick.
    ///
    /// <para>
    /// The knockback direction is the normalized impact direction received from the
    /// <see cref="DamageRequest"/>. The receiver should move in the <b>opposite</b>
    /// direction (i.e., <c>-direction</c>). Implementations are responsible for
    /// inverting the direction.
    /// </para>
    /// </summary>
    /// <param name="impactDirection">
    /// Normalized world-space direction of the incoming hit.
    /// </param>
    /// <param name="force">
    /// Knockback force in world units per second. Scaled by the simulation delta time
    /// inside the implementation.
    /// </param>
    void ApplyKnockbackImpulse(Vector2 impactDirection, float force);
}
