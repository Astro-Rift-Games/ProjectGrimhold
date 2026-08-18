using UnityEngine;

/// <summary>
/// Marks a character entity as capable of receiving knockback impulses.
///
/// Implemented by <see cref="CharacterBase"/> subclasses (PlayerCharacter, EnemyCharacter).
/// The implementation locates the entity's <see cref="IKnockbackMotor"/> and delegates
/// the impulse to it.
///
/// <para>
/// Called by <see cref="DamageResolver"/> immediately after a <see cref="DamageResult"/>
/// is applied successfully, within the same <c>FixedUpdateNetwork</c> tick.
/// Only executes under State Authority.
/// </para>
/// </summary>
public interface IKnockbackReceiver
{
    /// <summary>
    /// Delivers a knockback impulse originating from the given impact direction.
    /// The receiver is displaced away from the impact (i.e., in <c>-impactDirection</c>).
    /// </summary>
    /// <param name="impactDirection">
    /// Normalized world-space direction of the incoming attack, sourced from
    /// <see cref="DamageRequest.Direction"/>.
    /// </param>
    /// <param name="force">
    /// Knockback magnitude in world units per second.
    /// </param>
    void ReceiveKnockback(Vector2 impactDirection, float force);
}
