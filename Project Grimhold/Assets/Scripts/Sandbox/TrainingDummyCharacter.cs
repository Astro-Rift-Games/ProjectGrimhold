#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// Dev-only invulnerable target for ability testing. It accepts damage requests and
/// reports the mitigated amount as applied, records them in <see cref="DamageLog"/>,
/// but never changes Health and never dies.
/// </summary>
[DisallowMultipleComponent]
public sealed class TrainingDummyCharacter : CharacterBase, IAbilityEnemyTarget
{
    /// <summary>Damage recorded on the State Authority instance.</summary>
    public DummyDamageLog DamageLog { get; } = new DummyDamageLog();

    public override bool CanReceiveDamage => true;

    /// <summary>
    /// Intercepts damage before Health is touched, so Health stays at its maximum.
    /// Runs only on State Authority (guarded by the base ApplyDamage).
    /// </summary>
    protected override bool TryApplyAlternateDamage(
        in DamageRequest request,
        float mitigatedDamage,
        out DamageResult result)
    {
        DamageLog.Record(mitigatedDamage);
        result = new DamageResult(Id, true, mitigatedDamage, Health, false, DamageFailureReason.None);
        return true;
    }

    protected override bool TryInterceptFatalDamage(in DamageRequest request) => true;

    protected override void HandleDeath()
    {
    }
}
#endif
