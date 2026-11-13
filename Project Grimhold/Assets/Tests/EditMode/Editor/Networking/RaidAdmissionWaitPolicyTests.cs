#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using MatchPhase = NetworkMatchController.MatchPhase;

/// <summary>FIX-03: when a connected Client stops waiting for Raid admission (S7, S9).</summary>
public sealed class RaidAdmissionWaitPolicyTests
{
    private const float Grace = RaidAdmissionWindows.ClientAdmissionClosedGraceSeconds;

    private RaidAdmissionWaitState _state;

    [SetUp]
    public void SetUp() => _state = default;

    private RaidAdmissionWaitDecision Evaluate(
        float now = 0f,
        bool localAdmitted = false,
        bool localParticipantPresent = false,
        bool matchObserved = true,
        MatchPhase phase = MatchPhase.WaitingForPlayers,
        RaidClosureReason closureReason = RaidClosureReason.NaturalCompletion) =>
        RaidAdmissionWaitPolicy.Evaluate(
            ref _state, now, localAdmitted, localParticipantPresent, matchObserved, phase, closureReason);

    [Test]
    public void WaitingForPlayers_Continues()
    {
        Assert.That(Evaluate(), Is.EqualTo(RaidAdmissionWaitDecision.Continue));
    }

    [Test]
    public void MatchNotReplicatedYet_Continues()
    {
        // Until the controller is bound on this peer its phase and closure fields mean nothing.
        Assert.That(
            Evaluate(matchObserved: false, phase: MatchPhase.Closing, closureReason: RaidClosureReason.BootstrapFailure),
            Is.EqualTo(RaidAdmissionWaitDecision.Continue));
    }

    [Test]
    public void BootstrapFailureClosure_AbortsImmediately()
    {
        Assert.That(
            Evaluate(phase: MatchPhase.Closing, closureReason: RaidClosureReason.BootstrapFailure),
            Is.EqualTo(RaidAdmissionWaitDecision.Abort));
    }

    [Test]
    public void BootstrapFailureClosure_AbortsEvenWithALocalParticipantPresent()
    {
        Assert.That(
            Evaluate(localParticipantPresent: true, phase: MatchPhase.Closing, closureReason: RaidClosureReason.BootstrapFailure),
            Is.EqualTo(RaidAdmissionWaitDecision.Abort));
    }

    [TestCase(MatchPhase.Starting)]
    [TestCase(MatchPhase.InProgress)]
    [TestCase(MatchPhase.Closing)]
    [TestCase(MatchPhase.Finished)]
    public void Admitted_IsReportedEvenWhenThePhaseAlreadyMovedOn(MatchPhase phase)
    {
        // Starting -> InProgress can happen within one tick of the last admission.
        Assert.That(
            Evaluate(localAdmitted: true, phase: phase, closureReason: RaidClosureReason.BootstrapFailure),
            Is.EqualTo(RaidAdmissionWaitDecision.Admitted));
    }

    [Test]
    public void Admitted_WhileWaitingForPlayers_IsAdmitted()
    {
        Assert.That(Evaluate(localAdmitted: true), Is.EqualTo(RaidAdmissionWaitDecision.Admitted));
    }

    [TestCase(MatchPhase.Starting)]
    [TestCase(MatchPhase.InProgress)]
    [TestCase(MatchPhase.Closing)]
    [TestCase(MatchPhase.Finished)]
    public void PhaseMovedOnWithoutParticipant_FirstObservation_Continues(MatchPhase phase)
    {
        // The participant may simply replicate a poll after the phase (A1).
        Assert.That(Evaluate(now: 10f, phase: phase), Is.EqualTo(RaidAdmissionWaitDecision.Continue));
    }

    [Test]
    public void PhaseMovedOnWithoutParticipant_HeldShorterThanTheGrace_Continues()
    {
        Evaluate(now: 10f, phase: MatchPhase.InProgress);

        Assert.That(
            Evaluate(now: 10f + Grace - 0.01f, phase: MatchPhase.InProgress),
            Is.EqualTo(RaidAdmissionWaitDecision.Continue));
    }

    [TestCase(MatchPhase.Starting)]
    [TestCase(MatchPhase.InProgress)]
    [TestCase(MatchPhase.Closing)]
    [TestCase(MatchPhase.Finished)]
    public void PhaseMovedOnWithoutParticipant_HeldForTheGrace_Aborts(MatchPhase phase)
    {
        Evaluate(now: 10f, phase: phase);

        Assert.That(Evaluate(now: 10f + Grace, phase: phase), Is.EqualTo(RaidAdmissionWaitDecision.Abort));
    }

    [Test]
    public void NaturalCompletionClosing_WithoutLocalParticipant_AbortsAfterTheGrace()
    {
        Evaluate(now: 4f, phase: MatchPhase.Closing, closureReason: RaidClosureReason.NaturalCompletion);

        Assert.That(
            Evaluate(now: 4f + Grace, phase: MatchPhase.Closing, closureReason: RaidClosureReason.NaturalCompletion),
            Is.EqualTo(RaidAdmissionWaitDecision.Abort));
    }

    [Test]
    public void ParticipantAppearsDuringTheGrace_Continues_AndThenAdmits()
    {
        Evaluate(now: 10f, phase: MatchPhase.InProgress);

        Assert.That(
            Evaluate(now: 10f + Grace - 0.1f, localParticipantPresent: true, phase: MatchPhase.InProgress),
            Is.EqualTo(RaidAdmissionWaitDecision.Continue));
        Assert.That(
            Evaluate(now: 10f + Grace + 5f, localAdmitted: true, localParticipantPresent: true, phase: MatchPhase.InProgress),
            Is.EqualTo(RaidAdmissionWaitDecision.Admitted));
    }

    [Test]
    public void ParticipantPresent_PhaseMovedOn_NeverAbortsByGraceAlone()
    {
        Evaluate(now: 10f, localParticipantPresent: true, phase: MatchPhase.InProgress);

        Assert.That(
            Evaluate(now: 10f + Grace * 10f, localParticipantPresent: true, phase: MatchPhase.InProgress),
            Is.EqualTo(RaidAdmissionWaitDecision.Continue));
    }

    [Test]
    public void ParticipantAppearingResetsTheGraceClock()
    {
        Evaluate(now: 10f, phase: MatchPhase.InProgress);
        Evaluate(now: 10f + Grace - 0.1f, localParticipantPresent: true, phase: MatchPhase.InProgress);

        // The condition restarts when the participant disappears again: the old start must not count.
        Assert.That(
            Evaluate(now: 10f + Grace + 1f, phase: MatchPhase.InProgress),
            Is.EqualTo(RaidAdmissionWaitDecision.Continue));
        Assert.That(
            Evaluate(now: 10f + Grace + 1f + Grace, phase: MatchPhase.InProgress),
            Is.EqualTo(RaidAdmissionWaitDecision.Abort));
    }

    [Test]
    public void BackToWaitingForPlayers_ResetsTheGraceClock()
    {
        Evaluate(now: 10f, phase: MatchPhase.Starting);
        Evaluate(now: 10.2f, phase: MatchPhase.WaitingForPlayers);

        Assert.That(
            Evaluate(now: 10f + Grace, phase: MatchPhase.Starting),
            Is.EqualTo(RaidAdmissionWaitDecision.Continue));
    }

    [Test]
    public void UnobservedMatch_ResetsTheGraceClock()
    {
        Evaluate(now: 10f, phase: MatchPhase.InProgress);
        Evaluate(now: 10.2f, matchObserved: false, phase: MatchPhase.InProgress);

        Assert.That(
            Evaluate(now: 10f + Grace, phase: MatchPhase.InProgress),
            Is.EqualTo(RaidAdmissionWaitDecision.Continue));
    }
}
#endif
