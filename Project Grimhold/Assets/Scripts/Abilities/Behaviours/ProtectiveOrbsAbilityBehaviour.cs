using UnityEngine;

/// <summary>
/// Self ability: instant acceptance activates the independent avatar-owned orb effect.
/// No target search, projectile spawn, persistent execution, or generic player damage interception.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProtectiveOrbsAbilityBehaviour : AbilityExecutionBehaviour
{
    [SerializeField] private ProtectiveOrbsNetworkEffect _effect;
    [SerializeField, Range(1, 32)] private int _orbCount = 8;
    [Tooltip("Provisional balance: radius around the caster, independent of the surviving count.")]
    [SerializeField, Min(0.01f)] private float _orbitRadius = 1f;
    [SerializeField] private float _angularSpeedDegreesPerSecond = 30f;
    [Tooltip("Zero means permanent; positive durations expire on a Fusion tick deadline.")]
    [SerializeField, Min(0f)] private float _lifetimeSeconds = 15f;
    [Tooltip("Provisional balance: each contacting orb deals this damage, then disappears.")]
    [SerializeField, Min(0f)] private float _contactDamage = 3f;
    [SerializeField] private DamageType _damageType = DamageType.Magical;

    public override bool TryPlanStart(in AbilityExecutionContext context, out AbilityExecutionPlan plan)
    {
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (!TryValidateConfiguration(out string error))
        {
            Debug.LogError($"{nameof(ProtectiveOrbsAbilityBehaviour)} configuration is invalid: {error}", this);
            return true; // InvalidPlan: all checks happen before payment.
        }
        if (context.Character == null || context.Character.gameObject != gameObject) return false;
        if (!_effect.CanActivate)
        {
            Debug.LogError($"{nameof(ProtectiveOrbsAbilityBehaviour)} requires a running authoritative effect with runner registry and local slot geometry before payment.", this);
            return true;
        }
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Executing);
        return true;
    }

    public override void Begin(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        if (context.Runner == null || !context.Runner.IsSimulationUpdating || !context.Runner.IsForward ||
            context.Character == null || context.Character.gameObject != gameObject ||
            !context.Character.HasStateAuthority || _effect == null ||
            _effect.WasActivatedBy(snapshot.Sequence)) return;
        if (!_effect.TryActivate(snapshot.Sequence, _orbCount, _orbitRadius, _angularSpeedDegreesPerSecond,
                _lifetimeSeconds, _contactDamage, _damageType))
            Debug.LogError($"{nameof(ProtectiveOrbsAbilityBehaviour)} could not activate its accepted effect; cost and cooldown stay spent.", this);
    }

    public override AbilityExecutionPlan Simulate(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot) =>
        new AbilityExecutionPlan(AbilityExecutionPhase.Idle);

    public override bool CanInterrupt(AbilityExecutionStopReason reason) => false;

    public override void Stop(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot,
        AbilityExecutionStopReason reason)
    {
        // Execution completion/interruption never controls the already-created effect.
    }

    public override void Rebind(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        // Serialized references and the effect's copied state are sufficient; never activate again.
    }

    private bool TryValidateConfiguration(out string error)
    {
        if (!ProtectiveOrbsRules.TryValidateConfiguration(_orbCount, _orbitRadius,
                _angularSpeedDegreesPerSecond, _lifetimeSeconds, out error)) return false;
        if (!AttackTiming.IsFinite(_contactDamage) || _contactDamage < 0f ||
            !System.Enum.IsDefined(typeof(DamageType), _damageType))
        {
            error = "contact damage must be finite and nonnegative with a valid DamageType.";
            return false;
        }
        if (_effect == null || _effect.gameObject != gameObject)
        {
            error = "assign the same-root ProtectiveOrbsNetworkEffect.";
            return false;
        }
        return _effect.TryValidateDependencies(out error);
    }
}
