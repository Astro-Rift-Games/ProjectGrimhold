using NUnit.Framework;

public class DungeonPressureClockTests
{
    private const float DeltaTime = 1f / 60f; // 60hz tick rate
    
    [Test]
    public void Step_NotStarted_DoesNotConsumeTicks()
    {
        int remainingTicks = 600;
        DungeonPressureState state = DungeonPressureState.NotStarted;
        DungeonPressurePhase phase = DungeonPressurePhase.Normal;

        DungeonPressureClock.Step(ref remainingTicks, ref state, ref phase, false, false, 300, 120, DeltaTime);

        Assert.AreEqual(DungeonPressureState.NotStarted, state);
        Assert.AreEqual(600, remainingTicks);
    }

    [Test]
    public void Step_NotStarted_StartsWhenRaidInProgress()
    {
        int remainingTicks = 600;
        DungeonPressureState state = DungeonPressureState.NotStarted;
        DungeonPressurePhase phase = DungeonPressurePhase.Normal;

        DungeonPressureClock.Step(ref remainingTicks, ref state, ref phase, true, false, 300, 120, DeltaTime);

        Assert.AreEqual(DungeonPressureState.Running, state);
        Assert.AreEqual(600, remainingTicks); // Tick is not consumed in the same step it starts
    }

    [Test]
    public void Step_Running_ConsumesTicks()
    {
        int remainingTicks = 600;
        DungeonPressureState state = DungeonPressureState.Running;
        DungeonPressurePhase phase = DungeonPressurePhase.Normal;

        DungeonPressureClock.Step(ref remainingTicks, ref state, ref phase, true, false, 300, 120, DeltaTime);

        Assert.AreEqual(DungeonPressureState.Running, state);
        Assert.AreEqual(599, remainingTicks);
    }

    [Test]
    public void Step_Running_StopsWhenRaidClosingOrFinished()
    {
        int remainingTicks = 600;
        DungeonPressureState state = DungeonPressureState.Running;
        DungeonPressurePhase phase = DungeonPressurePhase.Normal;

        DungeonPressureClock.Step(ref remainingTicks, ref state, ref phase, true, true, 300, 120, DeltaTime);

        Assert.AreEqual(DungeonPressureState.Stopped, state);
        Assert.AreEqual(600, remainingTicks); // Not consumed
    }

    [Test]
    public void Step_Stopped_IsTerminal()
    {
        int remainingTicks = 600;
        DungeonPressureState state = DungeonPressureState.Stopped;
        DungeonPressurePhase phase = DungeonPressurePhase.Normal;

        // Try to start it again by passing isRaidInProgress = true
        DungeonPressureClock.Step(ref remainingTicks, ref state, ref phase, true, false, 300, 120, DeltaTime);

        Assert.AreEqual(DungeonPressureState.Stopped, state);
        Assert.AreEqual(600, remainingTicks);
    }

    [Test]
    public void Step_CollapsePhase_KeepsTicksAtZero()
    {
        int remainingTicks = 600;
        DungeonPressureState state = DungeonPressureState.Running;
        DungeonPressurePhase phase = DungeonPressurePhase.Collapse;

        DungeonPressureClock.Step(ref remainingTicks, ref state, ref phase, true, false, 300, 120, DeltaTime);

        Assert.AreEqual(0, remainingTicks);
        Assert.AreEqual(DungeonPressurePhase.Collapse, phase);
    }

    [Test]
    public void Step_PhaseNeverReverts()
    {
        int remainingTicks = 300 * 60; // 300 seconds
        DungeonPressureState state = DungeonPressureState.Running;
        DungeonPressurePhase phase = DungeonPressurePhase.CriticalPressure;

        // Thresholds imply it should be Normal or Reinforcements, but it's already Critical
        DungeonPressureClock.Step(ref remainingTicks, ref state, ref phase, true, false, 300, 120, DeltaTime);

        Assert.AreEqual(DungeonPressurePhase.CriticalPressure, phase);
    }
}
