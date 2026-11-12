using NUnit.Framework;

public sealed class RaidLossNoticePolicyTests
{
    [TestCase(RaidClosureReason.BootstrapFailure, RaidLossNoticeReason.RaidStartFailed)]
    public void ReasonForClosure_MapsTechnicalClosure(RaidClosureReason closure, RaidLossNoticeReason expected)
    {
        Assert.That(RaidLossNoticePolicy.ReasonForClosure(closure), Is.EqualTo(expected));
    }

    [Test]
    public void ReasonForClosure_NaturalCompletionIsNotATechnicalLoss()
    {
        // Abandon, Defeat and Extraction already show their Results inside the Raid.
        Assert.That(RaidLossNoticePolicy.ReasonForClosure(RaidClosureReason.NaturalCompletion), Is.Null);
    }

    [TestCase(NetworkMatchController.MatchPhase.Closing)]
    [TestCase(NetworkMatchController.MatchPhase.Finished)]
    public void HostReturnsAutomatically_WhenBootstrapFailureClosesTheRaid(NetworkMatchController.MatchPhase phase)
    {
        // The aborted participant has no Results screen and no accepted terminal result, so without
        // this the Solo Host is left in a dead scene with no Return.
        Assert.That(
            RaidLossNoticePolicy.ShouldReturnHostAfterTechnicalClosure(
                true, true, phase, RaidClosureReason.BootstrapFailure),
            Is.True);
    }

    [Test]
    public void HostDoesNotReturnAutomatically_ForNaturalCompletion()
    {
        Assert.That(
            RaidLossNoticePolicy.ShouldReturnHostAfterTechnicalClosure(
                true, true, NetworkMatchController.MatchPhase.Finished, RaidClosureReason.NaturalCompletion),
            Is.False);
    }

    [TestCase(NetworkMatchController.MatchPhase.WaitingForPlayers)]
    [TestCase(NetworkMatchController.MatchPhase.Starting)]
    [TestCase(NetworkMatchController.MatchPhase.InProgress)]
    public void HostDoesNotReturnAutomatically_BeforeTheClosureStarts(NetworkMatchController.MatchPhase phase)
    {
        Assert.That(
            RaidLossNoticePolicy.ShouldReturnHostAfterTechnicalClosure(
                true, true, phase, RaidClosureReason.BootstrapFailure),
            Is.False);
    }

    [Test]
    public void OnlyTheServerInTheRaidStateReturnsAutomatically()
    {
        const NetworkMatchController.MatchPhase phase = NetworkMatchController.MatchPhase.Finished;
        Assert.That(
            RaidLossNoticePolicy.ShouldReturnHostAfterTechnicalClosure(
                false, true, phase, RaidClosureReason.BootstrapFailure),
            Is.False);
        Assert.That(
            RaidLossNoticePolicy.ShouldReturnHostAfterTechnicalClosure(
                true, false, phase, RaidClosureReason.BootstrapFailure),
            Is.False);
    }

    [Test]
    public void ConnectionLoss_IsReported_WhenNoParticipationIsRetainedAndNoHandoff()
    {
        Assert.That(RaidLossNoticePolicy.ShouldReportConnectionLoss(false, false), Is.True);
    }

    [Test]
    public void ConnectionLoss_IsNotReported_WhenAReconnectTicketKeepsTheParticipation()
    {
        // Reason=Error on a Client preserves the ticket: the loadout is not consumed.
        Assert.That(RaidLossNoticePolicy.ShouldReportConnectionLoss(false, true), Is.False);
    }

    [Test]
    public void ConnectionLoss_IsNotReported_ForAnOutgoingHostHandoff()
    {
        // The outgoing Host already holds an accepted terminal result with Results shown.
        Assert.That(RaidLossNoticePolicy.ShouldReportConnectionLoss(true, false), Is.False);
    }
}
