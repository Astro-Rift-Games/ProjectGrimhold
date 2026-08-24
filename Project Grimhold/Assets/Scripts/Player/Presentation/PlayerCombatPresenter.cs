using UnityEngine;

/// <summary>
/// Presenter component responsible for coordinating player combat presentation.
/// Suppresses the legacy procedural weapon swing from <see cref="CombatPresenterBase"/>
/// since <see cref="PlayerWeaponPresenter"/> now fully handles dynamic weapon presentation
/// based on the equipped weapon definition.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCombatPresenter : CombatPresenterBase
{
    protected override void OnAttackPerformed(AttackPerformedEvent attackEvent)
    {
        // Suppressed! PlayerWeaponPresenter handles this.
    }

    protected override void LateUpdate()
    {
        // Suppressed! PlayerWeaponPresenter handles this.
    }
}
