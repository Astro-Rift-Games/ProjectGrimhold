#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Test-only caster-centered area behavior: validates at least one enemy in <see cref="Radius"/> at start and
/// rebuilds the targets around the caster when the Preparing phase resolves. No production placeholder exists.
/// </summary>
public sealed class TestAreaAbilityExecutionBehaviour : AbilityExecutionBehaviour
{
    public float Radius = 3f;
    public float PreparingSeconds = 0.5f;
    public int Begins;
    public int Resolutions;
    public int Stops;
    public AbilityExecutionStopReason LastStop;
    public readonly List<int> StartTargetIds = new();
    public readonly List<int> ResolvedTargetIds = new();
    private readonly List<AttackTarget> _targets = new();

    public override bool TryPlanStart(in AbilityExecutionContext context, out AbilityExecutionPlan plan)
    {
        plan = new AbilityExecutionPlan(AbilityExecutionPhase.Preparing, PreparingSeconds);
        if (!context.TryFindEnemiesInArea(Radius, _targets) || _targets.Count == 0) return false;
        Copy(StartTargetIds);
        return true;
    }

    public override void Begin(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot) => Begins++;

    public override AbilityExecutionPlan Simulate(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        if (snapshot.Phase != AbilityExecutionPhase.Preparing || !snapshot.PhaseDeadline.Expired(context.Runner))
            return new AbilityExecutionPlan(snapshot.Phase);
        // Resolution: targets are rebuilt around the caster's current position and applied once.
        context.TryFindEnemiesInArea(Radius, _targets);
        Copy(ResolvedTargetIds);
        Resolutions++;
        return new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
    }

    public override bool CanInterrupt(AbilityExecutionStopReason reason) => true;

    public override void Stop(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot,
        AbilityExecutionStopReason reason)
    {
        Stops++;
        LastStop = reason;
    }

    public override void Rebind(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot) { }

    private void Copy(List<int> destination)
    {
        destination.Clear();
        for (int i = 0; i < _targets.Count; i++) destination.Add(_targets[i].TargetId.Value);
    }
}
#endif
