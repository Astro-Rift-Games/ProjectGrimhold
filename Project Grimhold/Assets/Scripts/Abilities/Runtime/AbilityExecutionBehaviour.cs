using UnityEngine;

/// <summary>Concrete, authored execution variation. Never owns payment, sequence or cooldown.</summary>
public abstract class AbilityExecutionBehaviour : MonoBehaviour
{
    [SerializeField] private AbilityDefinition _definition;
    public AbilityDefinition Definition => _definition;

    /// <summary>Side-effect-free preflight. Every normal start rejection belongs here.</summary>
    public abstract bool TryPlanStart(in AbilityExecutionContext context, out AbilityExecutionPlan plan);
    /// <summary>Runs only for a newly accepted sequence, never during restore/rebind.</summary>
    public abstract void Begin(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot);
    /// <summary>Returns the desired phase; Idle completes. An unchanged phase retains its deadline.</summary>
    public abstract AbilityExecutionPlan Simulate(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot);
    public abstract bool CanInterrupt(AbilityExecutionStopReason reason);
    public abstract void Stop(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot,
        AbilityExecutionStopReason reason);
    /// <summary>Reconstruct local references only. Must not replay accepted effects.</summary>
    public abstract void Rebind(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot);
}
