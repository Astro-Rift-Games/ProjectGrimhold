using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Restoration (Restauración): after a preparation it heals every valid ally inside a caster-centered area at
/// resolution. The caster is part of its own ally set. A valid target is alive, not Downed and missing Health;
/// the heal is a flat amount and never revives. Targets are determined again at resolution; the start only
/// requires one. The preparation is interruptible and has no lasting state, so there is nothing to release on stop.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestorationAbilityBehaviour : AbilityExecutionBehaviour
{
    private const int InitialTargetCapacity = 8;

    [Header("Area")]
    [Tooltip("Radius of the caster-centered area. The target layers live on the caster's AbilityAreaTargetFinder.")]
    [SerializeField, Min(0.01f)] private float _radius = 3f;
    [Tooltip("Seconds between the accepted start and the heal. The preparation is interruptible.")]
    [SerializeField, Min(0.01f)] private float _preparationSeconds = 1f;
    [Tooltip("Voluntary movement speed multiplier while preparing (Game Design: reduced movement). Provisional value.")]
    [SerializeField, Range(0.01f, 1f)] private float _preparationMovementSpeedMultiplier = 0.5f;

    [Header("Heal")]
    [Tooltip("Flat Health restored to each valid ally; no attribute scaling. Provisional balance value (same as the health potion), not a finalized Game Design number.")]
    [SerializeField, Min(0.01f)] private float _healAmount = 30f;

    // Reused for both the start validation and the resolution: the finder copies into it on every call.
    private readonly List<PlayerCharacter> _allies = new(InitialTargetCapacity);

    private uint _lastResolvedSequence;

    public override bool TryPlanStart(in AbilityExecutionContext context, out AbilityExecutionPlan plan)
    {
        // An unusable configuration or unavailable targeting is not a normal rejection: it yields an invalid
        // plan (a configuration error). Having no valid ally with missing Health is the normal rejection.
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (!IsConfigurationValid()) return true;
        if (!context.TryFindAlliesInArea(_radius, _allies))
        {
            Debug.LogError($"{nameof(RestorationAbilityBehaviour)} requires a configured {nameof(AbilityAreaTargetFinder)} on the caster.", this);
            return true;
        }
        if (!HasRestorableAlly()) return false;
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Preparing, _preparationSeconds);
        return true;
    }

    public override void Begin(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        // Nothing to prepare: allies are determined again at resolution.
    }

    public override AbilityExecutionPlan Simulate(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        if (!snapshot.PhaseDeadline.Expired(context.Runner)) return new AbilityExecutionPlan(snapshot.Phase);

        // Returning Idle completes the execution: cost and cooldown stay spent whatever the resolution found.
        var idle = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (snapshot.Sequence == _lastResolvedSequence) return idle;
        _lastResolvedSequence = snapshot.Sequence;
        if (!IsConfigurationValid()) return idle;
        Resolve(context);
        return idle;
    }

    public override bool CanInterrupt(AbilityExecutionStopReason reason) =>
        reason == AbilityExecutionStopReason.Knockback || reason == AbilityExecutionStopReason.Stun;

    public override float GetVoluntaryMovementSpeedMultiplier(in AbilityExecutionSnapshot snapshot) =>
        snapshot.Phase == AbilityExecutionPhase.Preparing ? _preparationMovementSpeedMultiplier : 1f;

    public override void Stop(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot,
        AbilityExecutionStopReason reason)
    {
        // Nothing to release: the preparation holds no lasting state and the heal leaves no effect owner.
    }

    public override void Rebind(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        // No local references are kept.
    }

    private bool IsConfigurationValid()
    {
        if (RestorationRules.TryValidateConfiguration(_radius, _healAmount, _preparationSeconds,
                out string error)) return true;
        Debug.LogError($"{nameof(RestorationAbilityBehaviour)} configuration is invalid: {error}", this);
        return false;
    }

    private bool HasRestorableAlly()
    {
        for (int i = 0; i < _allies.Count; i++)
            if (CanReceiveRestoration(_allies[i])) return true;
        return false;
    }

    private static bool CanReceiveRestoration(PlayerCharacter ally) =>
        RestorationRules.CanReceiveRestoration(ally.IsAlive, ally.IsDowned, ally.Health, ally.MaxHealth);

    /// <summary>
    /// Rebuilds the allies around the caster's current position and heals each restorable one, in the
    /// deterministic order the finder returns them, within this same tick. Health clamping, authority and the
    /// Downed refusal are enforced by <see cref="CharacterBase.ApplyHealing"/>; no healing means no effect.
    /// </summary>
    private void Resolve(in AbilityExecutionContext context)
    {
        if (!context.TryFindAlliesInArea(_radius, _allies))
        {
            Debug.LogError($"{nameof(RestorationAbilityBehaviour)} could not rebuild its targets; the heal has no effect.", this);
            return;
        }

        for (int i = 0; i < _allies.Count; i++)
        {
            PlayerCharacter ally = _allies[i];
            if (!CanReceiveRestoration(ally)) continue;
            ally.ApplyHealing(new HealRequest(_healAmount));
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _radius = Mathf.Max(0.01f, _radius);
        _preparationSeconds = Mathf.Max(0.01f, _preparationSeconds);
        _healAmount = Mathf.Max(0.01f, _healAmount);
        _preparationMovementSpeedMultiplier = Mathf.Clamp(_preparationMovementSpeedMultiplier, 0.01f, 1f);
    }
#endif
}
