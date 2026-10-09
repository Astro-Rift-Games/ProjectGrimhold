using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Seismic Strike (Golpe Sísmico): after a preparation it releases a shockwave centered on the caster's position
/// at resolution. Every valid enemy inside the radius at that moment is damaged and pushed radially away from
/// the caster, all in the same tick. Targets are determined again at resolution; the start only requires one.
/// The preparation is interruptible and has no lasting state, so there is nothing to release on stop.
/// Knockback immunity and Stun are deliberately not applied: neither system exists yet.
/// </summary>
[DisallowMultipleComponent]
public sealed class SeismicStrikeAbilityBehaviour : AbilityExecutionBehaviour
{
    private const int InitialTargetCapacity = 16;

    [Header("Shockwave")]
    [Tooltip("Radius of the caster-centered area. The target layers live on the caster's AbilityAreaTargetFinder.")]
    [SerializeField, Min(0.01f)] private float _radius = 3f;
    [Tooltip("Seconds between the accepted start and the shockwave. The preparation is interruptible.")]
    [SerializeField, Min(0.01f)] private float _preparationSeconds = 0.75f;

    [Header("Impact")]
    [Tooltip("Zero is allowed: enemies are still pushed, without damage.")]
    [SerializeField, Min(0f)] private float _damage = 5f;
    [SerializeField] private DamageType _damageType = DamageType.Physical;
    [SerializeField, Min(0f)] private float _knockbackForce = 8f;

    // Reused for both the start validation and the resolution: the finder copies into it on every call.
    private readonly List<AttackTarget> _targets = new(InitialTargetCapacity);

    private PlayerCharacter _character;
    private IDamageResolver _damageResolver;
    private EntityRegistry _registry;
    private NetworkRunner _registryRunner;
    private uint _lastResolvedSequence;

    public override bool TryPlanStart(in AbilityExecutionContext context, out AbilityExecutionPlan plan)
    {
        // An unusable configuration or unavailable targeting is not a normal rejection: it yields an invalid
        // plan (a configuration error). Having no valid enemy is the normal rejection.
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (!IsConfigurationValid()) return true;
        if (!context.TryFindEnemiesInArea(_radius, _targets))
        {
            Debug.LogError($"{nameof(SeismicStrikeAbilityBehaviour)} requires a configured {nameof(AbilityAreaTargetFinder)} on the caster.", this);
            return true;
        }
        if (_targets.Count == 0) return false;
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Preparing, _preparationSeconds);
        return true;
    }

    public override void Begin(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot) =>
        TryResolveReferences(context);

    public override AbilityExecutionPlan Simulate(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        if (!snapshot.PhaseDeadline.Expired(context.Runner)) return new AbilityExecutionPlan(snapshot.Phase);

        // Returning Idle completes the execution: cost and cooldown stay spent whatever the resolution found.
        var idle = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (snapshot.Sequence == _lastResolvedSequence) return idle;
        _lastResolvedSequence = snapshot.Sequence;
        if (!TryResolveReferences(context) || !IsConfigurationValid()) return idle;
        Resolve(context);
        return idle;
    }

    public override bool CanInterrupt(AbilityExecutionStopReason reason) =>
        reason == AbilityExecutionStopReason.Knockback || reason == AbilityExecutionStopReason.Stun;

    public override void Stop(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot,
        AbilityExecutionStopReason reason)
    {
        // Nothing to release: the preparation holds no lasting state and the shockwave leaves no effect owner.
    }

    public override void Rebind(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot) =>
        TryResolveReferences(context);

    private bool IsConfigurationValid()
    {
        if (SeismicStrikeRules.TryValidateConfiguration(_radius, _damage, _knockbackForce, _preparationSeconds,
                out string error)) return true;
        Debug.LogError($"{nameof(SeismicStrikeAbilityBehaviour)} configuration is invalid: {error}", this);
        return false;
    }

    private bool TryResolveReferences(in AbilityExecutionContext context)
    {
        PlayerCharacter character = context.Character;
        if (character == null) return false;
        if (_character != character)
        {
            _character = character;
            _damageResolver = character.GetComponent<IDamageResolver>() ??
                              character.GetComponentInChildren<IDamageResolver>() ??
                              character.GetComponentInParent<IDamageResolver>();
        }
        if (_registry == null || _registryRunner != context.Runner)
        {
            _registry = context.Runner != null ? context.Runner.GetComponent<EntityRegistry>() : null;
            _registryRunner = context.Runner;
        }
        return _registry != null;
    }

    /// <summary>
    /// Rebuilds the targets around the caster's current position and affects each one, in the deterministic
    /// order the finder returns them (distance, then EntityId), within this same tick.
    /// </summary>
    private void Resolve(in AbilityExecutionContext context)
    {
        if (!context.TryFindEnemiesInArea(_radius, _targets))
        {
            Debug.LogError($"{nameof(SeismicStrikeAbilityBehaviour)} could not rebuild its targets; the shockwave has no effect.", this);
            return;
        }

        Vector2 center = context.Character.transform.position;
        EntityId casterId = context.Character.Id;
        int tick = context.Runner.Tick.Raw;
        for (int i = 0; i < _targets.Count; i++)
        {
            AttackTarget target = _targets[i];
            if (!_registry.TryGetDamageable(target.TargetId, out IDamageable damageable) ||
                !_registry.TryGetTransform(target.TargetId, out Transform targetTransform)) continue;
            Vector2 direction = SeismicStrikeRules.ResolveKnockbackDirection(center, targetTransform.position);
            // EXTENSION POINT (knockback immunity): a target immune to Knockback must still take the damage.
            // No immunity system exists yet, so every valid target is pushed.
            if (!AbilityImpact.TryApply(_damageResolver, casterId, target.TargetId, damageable, _damage, _damageType,
                    _knockbackForce, direction, target.HitPoint, tick, this)) return;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _radius = Mathf.Max(0.01f, _radius);
        _preparationSeconds = Mathf.Max(0.01f, _preparationSeconds);
        _damage = Mathf.Max(0f, _damage);
        _knockbackForce = Mathf.Max(0f, _knockbackForce);
    }
#endif
}
