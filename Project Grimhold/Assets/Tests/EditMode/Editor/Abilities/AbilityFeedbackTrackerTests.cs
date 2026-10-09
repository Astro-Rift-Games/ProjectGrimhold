using NUnit.Framework;

public sealed class AbilityFeedbackTrackerTests
{
    [Test]
    public void Observe_BeforeBaseline_ReportsNothingAndAdoptsState()
    {
        var tracker = new AbilityFeedbackTracker();

        Assert.That(tracker.Observe(7, AbilityExecutionPhase.Executing), Is.EqualTo(AbilityFeedbackEdge.None));
        Assert.That(tracker.Observe(7, AbilityExecutionPhase.Idle), Is.EqualTo(AbilityFeedbackEdge.Ended),
            "Only transitions after the adopted baseline may produce feedback.");
    }

    [Test]
    public void Baseline_RestoredActiveExecution_DoesNotReplayStart()
    {
        var tracker = new AbilityFeedbackTracker();
        tracker.Baseline(5, AbilityExecutionPhase.Preparing);

        Assert.That(tracker.Observe(5, AbilityExecutionPhase.Preparing), Is.EqualTo(AbilityFeedbackEdge.None));
    }

    [Test]
    public void Observe_SequenceAdvances_ReportsStarted()
    {
        var tracker = new AbilityFeedbackTracker();
        tracker.Baseline(2, AbilityExecutionPhase.Idle);

        Assert.That(tracker.Observe(3, AbilityExecutionPhase.Preparing), Is.EqualTo(AbilityFeedbackEdge.Started));
        Assert.That(tracker.Observe(3, AbilityExecutionPhase.Preparing), Is.EqualTo(AbilityFeedbackEdge.None));
    }

    [Test]
    public void Observe_PreparingToExecuting_ReportsPhaseAdvanced()
    {
        var tracker = new AbilityFeedbackTracker();
        tracker.Baseline(3, AbilityExecutionPhase.Preparing);

        Assert.That(tracker.Observe(3, AbilityExecutionPhase.Executing), Is.EqualTo(AbilityFeedbackEdge.PhaseAdvanced));
    }

    [Test]
    public void Observe_ActiveToIdle_ReportsEndedOnce()
    {
        var tracker = new AbilityFeedbackTracker();
        tracker.Baseline(3, AbilityExecutionPhase.Executing);

        Assert.That(tracker.Observe(3, AbilityExecutionPhase.Idle), Is.EqualTo(AbilityFeedbackEdge.Ended));
        Assert.That(tracker.Observe(3, AbilityExecutionPhase.Idle), Is.EqualTo(AbilityFeedbackEdge.None));
    }

    [Test]
    public void Observe_InstantExecution_ReportsStartedAndEndedInOneObservation()
    {
        // A one-tick execution (e.g. an instant dash) can start and end between two observations.
        var tracker = new AbilityFeedbackTracker();
        tracker.Baseline(1, AbilityExecutionPhase.Idle);

        Assert.That(tracker.Observe(2, AbilityExecutionPhase.Idle),
            Is.EqualTo(AbilityFeedbackEdge.Started | AbilityFeedbackEdge.Ended));
    }

    [Test]
    public void Observe_SequenceJumpWhileActive_ReportsStartedAgain()
    {
        var tracker = new AbilityFeedbackTracker();
        tracker.Baseline(1, AbilityExecutionPhase.Executing);

        Assert.That(tracker.Observe(2, AbilityExecutionPhase.Preparing), Is.EqualTo(AbilityFeedbackEdge.Started));
    }

    [Test]
    public void Observe_SequenceMovesBackwards_RebaselinesWithoutFeedback()
    {
        // Slot content or state was replaced (sandbox override, restore): never replay old activations.
        var tracker = new AbilityFeedbackTracker();
        tracker.Baseline(9, AbilityExecutionPhase.Idle);

        Assert.That(tracker.Observe(0, AbilityExecutionPhase.Idle), Is.EqualTo(AbilityFeedbackEdge.None));
        Assert.That(tracker.Observe(1, AbilityExecutionPhase.Preparing), Is.EqualTo(AbilityFeedbackEdge.Started));
    }

    [Test]
    public void Reset_ForgetsBaseline_SoNextObservationIsSilent()
    {
        var tracker = new AbilityFeedbackTracker();
        tracker.Baseline(1, AbilityExecutionPhase.Idle);
        tracker.Reset();

        Assert.That(tracker.Observe(4, AbilityExecutionPhase.Executing), Is.EqualTo(AbilityFeedbackEdge.None));
    }
}
