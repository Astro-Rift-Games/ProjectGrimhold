using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Purification (Purificación): after a cast it removes every purifiable state from each valid ally inside a
/// caster-centered area at resolution and grants each a temporary protection against NEW purifiable states. The
/// caster is part of its own ally set. An ally is valid when it has an active purifiable state or can receive the
/// protection, which allows preventive use. Allies are determined again at resolution; the start only requires
/// one. The cast is interruptible and has no lasting state, so there is nothing to release on stop.
/// </summary>
[DisallowMultipleComponent]
public sealed class PurificationAbilityBehaviour : AbilityExecutionBehaviour
{
    private const int InitialTargetCapacity = 8;

    [Header("Area")]
    [Tooltip("Radius of the caster-centered area. The target layers live on the caster's AbilityAreaTargetFinder. Provisional value.")]
    [SerializeField, Min(0.01f)] private float _radius = 3f;
    [Tooltip("Seconds between the accepted start and the purification. The cast is interruptible. Provisional value.")]
    [SerializeField, Min(0.01f)] private float _castSeconds = 0.5f;

    [Header("Protection")]
    [Tooltip("Seconds during which NEW purifiable states cannot be applied to a purified ally. A new application refreshes it.")]
    [SerializeField, Min(0.01f)] private float _protectionSeconds = 5f;

    // Reused for both the start validation and the resolution: the finder copies into it on every call.
    private readonly List<PlayerCharacter> _allies = new(InitialTargetCapacity);

    private uint _lastResolvedSequence;

    public override bool TryPlanStart(in AbilityExecutionContext context, out AbilityExecutionPlan plan)
    {
        // An unusable configuration or unavailable targeting is not a normal rejection: it yields an invalid
        // plan (a configuration error). Having no valid ally is the normal rejection.
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (!IsConfigurationValid()) return true;
        if (!context.TryFindAlliesInArea(_radius, _allies))
        {
            Debug.LogError($"{nameof(PurificationAbilityBehaviour)} requires a configured {nameof(AbilityAreaTargetFinder)} on the caster.", this);
            return true;
        }

        // A Stunned caster cannot execute Purification (another ally may purify it). No player Stun state exists
        // yet, so there is nothing to check here; the common runtime's Stun gate owns that restriction once it does.
        if (!HasValidAlly()) return false;
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Preparing, _castSeconds);
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

    public override void Stop(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot,
        AbilityExecutionStopReason reason)
    {
        // Nothing to release: the cast holds no lasting state and the protection lives on each ally.
    }

    public override void Rebind(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        // No local references are kept.
    }

    private bool IsConfigurationValid()
    {
        if (PurificationRules.TryValidateConfiguration(_radius, _castSeconds, _protectionSeconds,
                out string error)) return true;
        Debug.LogError($"{nameof(PurificationAbilityBehaviour)} configuration is invalid: {error}", this);
        return false;
    }

    private bool HasValidAlly()
    {
        for (int i = 0; i < _allies.Count; i++)
            if (TryGetValidEffect(_allies[i], out _)) return true;
        return false;
    }

    /// <summary>
    /// Resolves the ally's effect and checks the particular target condition. An ally without the effect is a
    /// configuration error: it is logged with the ally as context and is not a valid target.
    /// </summary>
    private bool TryGetValidEffect(PlayerCharacter ally, out PurificationNetworkEffect effect)
    {
        if (!ally.TryGetComponent(out effect))
        {
            Debug.LogError($"{nameof(PurificationAbilityBehaviour)} found an ally without {nameof(PurificationNetworkEffect)}; it is skipped.", ally);
            return false;
        }
        return PurificationRules.IsValidTarget(effect.HasPurifiableState, effect.CanReceiveProtection);
    }

    /// <summary>
    /// Rebuilds the allies around the caster's current position and purifies each valid one, in the deterministic
    /// order the finder returns them, within this same tick. Authority is enforced by the effect.
    /// </summary>
    private void Resolve(in AbilityExecutionContext context)
    {
        if (!context.TryFindAlliesInArea(_radius, _allies))
        {
            Debug.LogError($"{nameof(PurificationAbilityBehaviour)} could not rebuild its targets; the purification has no effect.", this);
            return;
        }

        for (int i = 0; i < _allies.Count; i++)
        {
            if (!TryGetValidEffect(_allies[i], out PurificationNetworkEffect effect)) continue;
            effect.TryApply(_protectionSeconds);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _radius = Mathf.Max(0.01f, _radius);
        _castSeconds = Mathf.Max(0.01f, _castSeconds);
        _protectionSeconds = Mathf.Max(0.01f, _protectionSeconds);
    }
#endif
}
