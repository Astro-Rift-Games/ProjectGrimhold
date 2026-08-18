using UnityEngine;

/// <summary>
/// Marks an enemy AI as capable of receiving an aggro alert triggered by player damage.
///
/// Implemented by <see cref="EnemyMovementAIController"/>.
///
/// <para>
/// An aggro alert causes the receiver to immediately pursue the attacker for a
/// configurable duration, bypassing the normal line-of-sight requirement.
/// If the alert expires without the enemy establishing LOS, it reverts to its
/// previous state (Idle or Patrol).
/// </para>
///
/// <para>
/// Called by <see cref="DamageResolver"/> after a successful hit when the attacker
/// is a <see cref="PlayerCharacter"/>. Must be called under State Authority.
/// </para>
/// </summary>
public interface IAggroReceiver
{
    /// <summary>
    /// Signals this receiver that it was damaged by the given player entity.
    /// The receiver should immediately acquire the attacker as its active target
    /// and begin pursuit, ignoring line-of-sight for the configured alert duration.
    /// </summary>
    /// <param name="attackerId">EntityId of the player that dealt the damage.</param>
    /// <param name="attackerTransform">World Transform of the player. Must not be null.</param>
    void ReceiveAggroAlert(EntityId attackerId, Transform attackerTransform);
}
