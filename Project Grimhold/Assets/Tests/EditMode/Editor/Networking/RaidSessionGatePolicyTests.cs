#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

/// <summary>
/// FIX-51: the replacement Host must reopen the room for a retained participant even when the
/// match controller is not ready at seal time; the decision is deferred and re-applied.
/// </summary>
public sealed class RaidSessionGatePolicyTests
{
    private const NetworkMatchController.MatchPhase InProgress = NetworkMatchController.MatchPhase.InProgress;

    [Test]
    public void Evaluate_ReadyWithRetained_AppliesOpen()
    {
        RaidSessionGateDecision decision = RaidSessionGatePolicy.Evaluate(true, true, InProgress);

        Assert.IsTrue(decision.IsApplicable);
        Assert.IsTrue(decision.TargetIsOpen);
        Assert.AreEqual(RaidSessionGateDeferral.None, decision.Deferral);
    }

    [Test]
    public void Evaluate_ReadyWithoutRetained_AppliesClosed()
    {
        RaidSessionGateDecision decision = RaidSessionGatePolicy.Evaluate(false, true, InProgress);

        Assert.IsTrue(decision.IsApplicable);
        Assert.IsFalse(decision.TargetIsOpen);
    }

    [Test]
    public void Evaluate_NoStateAuthority_Defers()
    {
        RaidSessionGateDecision decision = RaidSessionGatePolicy.Evaluate(true, false, InProgress);

        Assert.IsFalse(decision.IsApplicable);
        Assert.AreEqual(RaidSessionGateDeferral.NoStateAuthority, decision.Deferral);
        Assert.IsTrue(decision.TargetIsOpen);
    }

    [TestCase(NetworkMatchController.MatchPhase.WaitingForPlayers)]
    [TestCase(NetworkMatchController.MatchPhase.Starting)]
    [TestCase(NetworkMatchController.MatchPhase.Closing)]
    [TestCase(NetworkMatchController.MatchPhase.Finished)]
    public void Evaluate_PhaseNotInProgress_Defers(NetworkMatchController.MatchPhase phase)
    {
        RaidSessionGateDecision decision = RaidSessionGatePolicy.Evaluate(true, true, phase);

        Assert.IsFalse(decision.IsApplicable);
        Assert.AreEqual(RaidSessionGateDeferral.PhaseNotInProgress, decision.Deferral);
    }

    [Test]
    public void Tracker_DeferredDecision_StaysPendingUntilApplied()
    {
        var tracker = new RaidSessionGateTracker();

        tracker.Record(RaidSessionGatePolicy.Evaluate(true, true, NetworkMatchController.MatchPhase.Starting));
        Assert.IsTrue(tracker.HasPendingDecision);

        tracker.Record(RaidSessionGatePolicy.Evaluate(true, true, InProgress));
        tracker.MarkApplied();
        Assert.IsFalse(tracker.HasPendingDecision);
    }

    [Test]
    public void Tracker_AppliedDecision_IsNotPending()
    {
        var tracker = new RaidSessionGateTracker();

        tracker.Record(RaidSessionGatePolicy.Evaluate(false, true, InProgress));

        Assert.IsFalse(tracker.HasPendingDecision);
    }

    [Test]
    public void Tracker_LogsOnlyWhenSignatureChanges()
    {
        var tracker = new RaidSessionGateTracker();

        Assert.IsTrue(tracker.ShouldLog("a"));
        Assert.IsFalse(tracker.ShouldLog("a"));
        Assert.IsTrue(tracker.ShouldLog("b"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Tracker_FirstDecision_AlwaysWrites(bool target)
    {
        var tracker = new RaidSessionGateTracker();

        Assert.IsTrue(tracker.ShouldWrite(target));
    }

    [Test]
    public void Tracker_SealCloseThenReopen_WritesEvenIfObservedIsStale()
    {
        var tracker = new RaidSessionGateTracker();
        tracker.MarkRequested(false);

        Assert.IsTrue(tracker.ShouldWrite(true));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Tracker_SameAsLastRequested_DoesNotRewrite(bool target)
    {
        var tracker = new RaidSessionGateTracker();
        tracker.MarkRequested(target);

        Assert.IsFalse(tracker.ShouldWrite(target));
    }

    [Test]
    public void Tracker_MarkRequested_ExposesLastRequestedValue()
    {
        var tracker = new RaidSessionGateTracker();
        Assert.IsNull(tracker.LastRequestedIsOpen);

        tracker.MarkRequested(true);

        Assert.AreEqual(true, tracker.LastRequestedIsOpen);
    }
}
#endif
