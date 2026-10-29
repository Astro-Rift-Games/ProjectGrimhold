using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Empower (Potenciar): instantly applies a temporary basic weapon attack damage bonus to every valid ally inside
/// a caster-centered area. The caster is part of its own ally set, so the start only requires the caster itself or
/// one teammate. Allies are determined again at resolution. Each ally owns its independent
/// <see cref="EmpowerNetworkEffect"/>; once applied it no longer depends on the caster or on this execution.
/// </summary>
[DisallowMultipleComponent]
public sealed class EmpowerAbilityBehaviour : AbilityExecutionBehaviour
{
    private const int InitialTargetCapacity = 8;

    [Header("Area")]
    [Tooltip("Radius of the caster-centered area. The target layers live on the caster's AbilityAreaTargetFinder. Provisional value.")]
    [SerializeField, Min(0.01f)] private float _radius = 3f;

    [Header("Effect")]
    [Tooltip("Fraction added to the damage of basic weapon attacks (0.3 = +30%). Never stacks: a new application refreshes it.")]
    [SerializeField, Min(0.0001f)] private float _damageBonusFraction = 0.3f;
    [SerializeField, Min(0.01f)] private float _durationSeconds = 10f;

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
            Debug.LogError($"{nameof(EmpowerAbilityBehaviour)} requires a configured {nameof(AbilityAreaTargetFinder)} on the caster.", this);
            return true;
        }
        if (_allies.Count == 0) return false;
        // Validate EVERY intended recipient before the runtime pays. This query never applies an effect.
        for (int i = 0; i < _allies.Count; i++)
        {
            PlayerCharacter ally = _allies[i];
            if (ally.TryGetComponent(out EmpowerNetworkEffect effect) && effect.IsReadyToApply) continue;
            Debug.LogError($"{nameof(EmpowerAbilityBehaviour)} configuration is invalid: every intended ally requires a ready same-root {nameof(EmpowerNetworkEffect)} before payment.", ally);
            return true; // Idle is InvalidPlan, not the normal no-target rejection.
        }
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Executing);
        return true;
    }

    public override void Begin(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        // Begin runs once per newly accepted sequence and never on restore/rebind; the sequence guard also keeps a
        // repeated call from refreshing the duration twice.
        if (context.Runner == null || !context.Runner.IsSimulationUpdating || !context.Runner.IsForward ||
            context.Character == null || !context.Character.HasStateAuthority ||
            snapshot.Sequence == _lastResolvedSequence) return;
        _lastResolvedSequence = snapshot.Sequence;
        if (!IsConfigurationValid()) return;
        Resolve(context);
    }

    public override AbilityExecutionPlan Simulate(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot) =>
        new AbilityExecutionPlan(AbilityExecutionPhase.Idle);

    public override bool CanInterrupt(AbilityExecutionStopReason reason) => false;

    public override void Stop(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot,
        AbilityExecutionStopReason reason)
    {
        // Execution completion/interruption never controls the already-applied effects.
    }

    public override void Rebind(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        // No local references are kept; the effects live on the allies and never apply again.
    }

    private bool IsConfigurationValid()
    {
        if (EmpowerRules.TryValidateConfiguration(_radius, _damageBonusFraction, _durationSeconds,
                out string error)) return true;
        Debug.LogError($"{nameof(EmpowerAbilityBehaviour)} configuration is invalid: {error}", this);
        return false;
    }

    /// <summary>
    /// Rebuilds the allies around the caster's current position and applies the effect to each one, in the
    /// deterministic order the finder returns them, within this same tick.
    /// </summary>
    private void Resolve(in AbilityExecutionContext context)
    {
        if (!context.TryFindAlliesInArea(_radius, _allies))
        {
            Debug.LogError($"{nameof(EmpowerAbilityBehaviour)} could not rebuild its targets; the ability has no effect.", this);
            return;
        }

        RaidParticipantId sourceId = default;
        if (PlayerPvpCombatCreditNetworkController.TryResolveSourceParticipant(context.Character, out var participant))
            sourceId = participant.RaidParticipantId;

        for (int i = 0; i < _allies.Count; i++)
        {
            PlayerCharacter ally = _allies[i];
            if (!ally.TryGetComponent(out EmpowerNetworkEffect effect))
            {
                Debug.LogError($"{nameof(EmpowerAbilityBehaviour)} found an ally without {nameof(EmpowerNetworkEffect)}; it is skipped.", ally);
                continue;
            }
            effect.TryApply(_damageBonusFraction, _durationSeconds, sourceId);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _radius = Mathf.Max(0.01f, _radius);
        _damageBonusFraction = Mathf.Max(0.0001f, _damageBonusFraction);
        _durationSeconds = Mathf.Max(0.01f, _durationSeconds);
    }
#endif
}
