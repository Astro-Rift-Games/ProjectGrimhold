#if UNITY_INCLUDE_TESTS
using UnityEngine;

/// <summary>Test-only execution; no production placeholder behavior is composed.</summary>
public sealed class TestAbilityExecutionBehaviour : AbilityExecutionBehaviour
{
    public bool RejectStart;
    public float PreparingSeconds;
    public bool Complete;
    public bool Interruptible = true;
    public int Begins;
    public int Stops;
    public int Rebinds;
    public AbilityExecutionStopReason LastStop;
    public Vector2 BeginAim;
    public Vector2 LastSimulateAim;
    public Vector2 LastRebindAim;
    public Vector2 LastStopAim;

    public override bool TryPlanStart(in AbilityExecutionContext context, out AbilityExecutionPlan plan)
    {
        plan = new AbilityExecutionPlan(PreparingSeconds > 0f ? AbilityExecutionPhase.Preparing :
            AbilityExecutionPhase.Executing, PreparingSeconds);
        return !RejectStart;
    }

    public override void Begin(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        Begins++;
        BeginAim = snapshot.AimDirection;
    }

    public override AbilityExecutionPlan Simulate(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        LastSimulateAim = snapshot.AimDirection;
        if (Complete) return new AbilityExecutionPlan(AbilityExecutionPhase.Idle);
        if (snapshot.Phase == AbilityExecutionPhase.Preparing && snapshot.PhaseDeadline.Expired(context.Runner))
            return new AbilityExecutionPlan(AbilityExecutionPhase.Executing);
        return new AbilityExecutionPlan(snapshot.Phase);
    }
    public override bool CanInterrupt(AbilityExecutionStopReason reason) => Interruptible;
    public override void Stop(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot,
        AbilityExecutionStopReason reason)
    {
        Stops++;
        LastStop = reason;
        LastStopAim = snapshot.AimDirection;
    }

    public override void Rebind(in AbilityExecutionContext context, in AbilityExecutionSnapshot snapshot)
    {
        Rebinds++;
        LastRebindAim = snapshot.AimDirection;
    }
}
#endif
