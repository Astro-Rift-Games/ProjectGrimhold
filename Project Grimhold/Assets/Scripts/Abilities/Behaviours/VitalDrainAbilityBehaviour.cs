using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PvE-only caster-centered drain. Rebuilds creature targets at resolution, then heals the caster from
/// confirmed actual damage. Payment, cooldown and copied preparation state belong to the common runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class VitalDrainAbilityBehaviour : AbilityExecutionBehaviour
{
    [Header("Area and preparation")]
    [Tooltip("Provisional radius, following existing area abilities. No default line-of-sight restriction.")]
    [SerializeField, Min(0.01f)] private float _radius = 3f;
    [Tooltip("Game Design baseline preparation (1 s); configurable.")]
    [SerializeField, Min(0.01f)] private float _preparationSeconds = 1f;

    [Header("Drain")]
    [Tooltip("Independent flat damage per creature. Provisional existing ability baseline, not final balance.")]
    [SerializeField, Min(0f)] private float _damage = 5f;
    [SerializeField] private DamageType _damageType = DamageType.Magical;
    [Tooltip("Fraction of summed actual damage restored to the caster. Provisional 50%, not final balance.")]
    [SerializeField, Min(0f)] private float _drainProportion = 0.5f;

    private readonly List<AttackTarget> _targets = new(16);
    private PlayerCharacter _character;
    private IDamageResolver _damageResolver;
    private uint _lastResolvedSequence;

    public override bool TryPlanStart(in AbilityExecutionContext context, out AbilityExecutionPlan plan)
    {
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (!IsConfigurationValid()) return true;
        if (!context.TryFindEnemiesInArea(_radius, _targets))
        {
            Debug.LogError($"{nameof(VitalDrainAbilityBehaviour)} requires a configured {nameof(AbilityAreaTargetFinder)} on the caster.", this);
            return true;
        }
        if (_targets.Count == 0) return false;
        if (!TryResolveReferences(context)) return true;
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Preparing, _preparationSeconds);
        return true;
    }

    public override void Begin(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot) =>
        TryResolveReferences(context);

    public override AbilityExecutionPlan Simulate(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        // The common runtime already enforces this boundary; keep direct calls from committing effects too.
        if (context.Runner == null || context.Character == null || !context.Character.HasStateAuthority ||
            !context.Runner.IsSimulationUpdating || !context.Runner.IsForward)
            return new AbilityExecutionPlan(snapshot.Phase);
        if (!snapshot.PhaseDeadline.Expired(context.Runner)) return new AbilityExecutionPlan(snapshot.Phase);
        var idle = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (snapshot.Sequence == _lastResolvedSequence) return idle;
        _lastResolvedSequence = snapshot.Sequence;
        if (!IsConfigurationValid() || !TryResolveReferences(context)) return idle;
        Resolve(context);
        return idle;
    }

    public override bool CanInterrupt(AbilityExecutionStopReason reason) =>
        reason == AbilityExecutionStopReason.Knockback || reason == AbilityExecutionStopReason.Stun;

    public override void Stop(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot,
        AbilityExecutionStopReason reason) { }

    // Restore/re-enable never resolves, pays, heals or restarts the copied deadline.
    public override void Rebind(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot) =>
        TryResolveReferences(context);

    private bool IsConfigurationValid()
    {
        if (VitalDrainRules.TryValidateConfiguration(_radius, _damage, _drainProportion, _preparationSeconds,
                out string error)) return true;
        Debug.LogError($"{nameof(VitalDrainAbilityBehaviour)} configuration is invalid: {error}", this);
        return false;
    }

    private bool TryResolveReferences(in AbilityExecutionContext context)
    {
        if (context.Character == null) return false;
        if (_character != context.Character || _damageResolver == null)
        {
            _character = context.Character;
            _damageResolver = _character.GetComponent<IDamageResolver>() ??
                              _character.GetComponentInChildren<IDamageResolver>() ??
                              _character.GetComponentInParent<IDamageResolver>();
        }
        if (_damageResolver != null) return true;
        Debug.LogError($"{nameof(VitalDrainAbilityBehaviour)} requires an {nameof(IDamageResolver)} on the caster to apply damage.", this);
        return false;
    }

    private void Resolve(in AbilityExecutionContext context)
    {
        if (!context.TryFindEnemiesInArea(_radius, _targets))
        {
            Debug.LogError($"{nameof(VitalDrainAbilityBehaviour)} could not rebuild its targets; the drain has no effect.", this);
            return;
        }

        double total = 0d;
        Vector2 center = context.Character.transform.position;
        for (int i = 0; i < _targets.Count; i++)
        {
            AttackTarget target = _targets[i];
            // Direct resolver use preserves attribution, mitigation, rewards and feedback, and retains its result.
            DamageResult result = _damageResolver.Resolve(new DamageRequest(context.Character.Id, target.TargetId,
                _damage, _damageType, target.HitPoint - center, target.HitPoint, context.Runner.Tick.Raw));
            total = VitalDrainRules.AddConfirmedDamage(total, result);
        }

        float healing = VitalDrainRules.CalculateHealing(total, _drainProportion,
            context.Character.Health, context.Character.MaxHealth);
        if (healing > 0f) context.Character.ApplyHealing(new HealRequest(healing));
    }
}
