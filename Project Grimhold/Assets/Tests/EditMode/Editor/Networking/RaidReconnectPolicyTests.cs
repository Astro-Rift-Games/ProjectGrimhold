using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class RaidReconnectPolicyTests
{
    private static RaidCode Code()
    {
        Assert.That(RaidCode.TryParse("123456", out RaidCode code), Is.True);
        return code;
    }

    private static readonly ProfileId Profile = new ProfileId("6af3ba84d4467f2fdbf3dd12");

    [Test]
    public void BuildTicket_CarriesActiveReservation()
    {
        RaidReconnectTicket ticket = RaidReconnectPolicy.BuildTicketAtUnexpectedShutdown(
            Code(), Profile, "gen", "session", "active-res", "persisted-res");

        Assert.That(ticket.ReservationId, Is.EqualTo("active-res"));
        Assert.That(ticket.ToPayload().ReservationId, Is.EqualTo("active-res"));
        Assert.That(ticket.IsValid, Is.True);
    }

    [Test]
    public void BuildTicket_FallsBackToPersistedReservation()
    {
        RaidReconnectTicket ticket = RaidReconnectPolicy.BuildTicketAtUnexpectedShutdown(
            Code(), Profile, "gen", "session", null, "persisted-res");

        Assert.That(ticket.ReservationId, Is.EqualTo("persisted-res"));
    }

    [Test]
    public void BuildTicket_NoReservationAnywhere_IsEmpty()
    {
        RaidReconnectTicket ticket = RaidReconnectPolicy.BuildTicketAtUnexpectedShutdown(
            Code(), Profile, "gen", "session", "", null);

        Assert.That(ticket.ReservationId, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ProofSent_SatisfiesHostCheck_ForSameReservation()
    {
        RaidReconnectTicket ticket = RaidReconnectPolicy.BuildTicketAtUnexpectedShutdown(
            Code(), Profile, "gen", "session", "8cc0a30b", null);

        Assert.That(NetworkSpawnManager.IsReconnectReservationProofValid("8cc0a30b", ticket.ReservationId), Is.True);
        Assert.That(NetworkSpawnManager.IsReconnectReservationProofValid("8cc0a30b", string.Empty), Is.False);
    }

    [Test]
    public void AdmissionFailure_RunnerEndedByHost_IsConnectionRefused_AndTerminal()
    {
        ShutdownReason reason = RaidReconnectPolicy.ClassifyAdmissionFailure(runnerEndedBeforeDeadline: true);

        Assert.That(reason, Is.EqualTo(ShutdownReason.ConnectionRefused));
        Assert.That(SessionConnectionCoordinator.ShouldDiscardReconnectTicket(reason, attemptThrew: false), Is.True);
        Assert.That(RaidReconnectPolicy.ShouldRetryAttempt(reason, 30f), Is.False);
    }

    [Test]
    public void AdmissionFailure_Timeout_IsNotTerminal()
    {
        ShutdownReason reason = RaidReconnectPolicy.ClassifyAdmissionFailure(runnerEndedBeforeDeadline: false);

        Assert.That(reason, Is.EqualTo(ShutdownReason.Error));
        Assert.That(RaidReconnectPolicy.ShouldRetryAttempt(reason, 30f), Is.True);
    }

    [Test]
    public void ShouldRetry_PreservesExistingBehavior()
    {
        Assert.That(RaidReconnectPolicy.ShouldRetryAttempt(ShutdownReason.GameNotFound, 30f), Is.False);
        Assert.That(RaidReconnectPolicy.ShouldRetryAttempt(ShutdownReason.GameClosed, 30f), Is.False);
        Assert.That(RaidReconnectPolicy.ShouldRetryAttempt(ShutdownReason.Ok, 0f), Is.False);
        Assert.That(RaidReconnectPolicy.ShouldRetryAttempt(ShutdownReason.Ok, 5f), Is.True);
    }

    [Test]
    public void DescribeProof_NeverExposesValue()
    {
        Assert.That(RaidReconnectPolicy.DescribeProof("secret"), Is.EqualTo("yes"));
        Assert.That(RaidReconnectPolicy.DescribeProof(""), Is.EqualTo("no"));
        Assert.That(RaidReconnectPolicy.DescribeProof(null), Is.EqualTo("no"));
    }
}
