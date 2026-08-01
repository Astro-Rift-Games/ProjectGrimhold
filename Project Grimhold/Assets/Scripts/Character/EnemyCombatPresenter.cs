using UnityEngine;

/// <summary>
/// Presenter component responsible for enemy attack animation driven by the Animator Controller.
///
/// Suppresses the procedural weapon swing from <see cref="CombatPresenterBase"/>.
/// The Animator Controller owns both the attack animation and the facing direction during attack
/// via the "IsAttacking" boolean — no temporal facing override is needed here.
///
/// Inherits event subscription and <see cref="CombatPresenterBase.CancelAndRestore"/> from
/// <see cref="CombatPresenterBase"/> so that <see cref="DefeatPresenterBase"/> can cancel
/// the attack presentation on death without changes.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyCombatPresenter : CombatPresenterBase
{
    /// <summary>
    /// Suppresses the procedural weapon swing inherited from the base class.
    /// The Animator Controller drives the attack animation and facing via "IsAttacking";
    /// no additional animator calls or direction overrides are required here.
    /// </summary>
    protected override void OnAttackPerformed(AttackPerformedEvent attackEvent)
    {
        // Intentionally empty.
        // Applying ApplyTemporalFacingDirection here would lock _temporalFacingDirection
        // permanently because LateUpdate is suppressed, preventing IsMoving from being
        // restored after the attack ends.
    }

    /// <summary>
    /// Enemy attack animation is owned by the Animator Controller. No per-frame update needed.
    /// </summary>
    protected override void LateUpdate()
    {
        // Intentionally empty.
        // The Animator Controller drives the attack animation via the IsAttacking boolean.
    }
}
