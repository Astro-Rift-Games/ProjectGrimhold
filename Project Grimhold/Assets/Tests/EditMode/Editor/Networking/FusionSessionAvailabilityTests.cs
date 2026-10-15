#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class FusionSessionAvailabilityTests
{
    [Test]
    public void RaidRuntimeCapacity_DerivesFromCanonicalSixteenParticipantRule()
    {
        Assert.That(FusionSessionLauncher.RaidPlayerCapacity, Is.EqualTo(16));
        Assert.That(FusionSessionLauncher.RaidPlayerCapacity, Is.EqualTo(RaidSessionRules.MaxParticipants));
    }

    [Test]
    public void GameNotFound_WaitsForHost()
    {
        Assert.That(FusionSessionLauncher.IsSessionAvailabilityPending(ShutdownReason.GameNotFound), Is.True);
    }

    [Test]
    public void GameClosed_WaitsForHost()
    {
        Assert.That(FusionSessionLauncher.IsSessionAvailabilityPending(ShutdownReason.GameClosed), Is.True);
    }

    [TestCase(ShutdownReason.Error)]
    [TestCase(ShutdownReason.IncompatibleConfiguration)]
    [TestCase(ShutdownReason.InvalidAuthentication)]
    [TestCase(ShutdownReason.GameIsFull)]
    public void DefinitiveConnectionFailure_DoesNotWaitForHost(ShutdownReason reason)
    {
        Assert.That(FusionSessionLauncher.IsSessionAvailabilityPending(reason), Is.False);
    }

    [Test]
    public void ExpectedCohortAdmission_RequiresEveryFrozenMember()
    {
        Assert.That(NetworkMatchController.IsExpectedCohortAdmitted(2, 1), Is.False);
        Assert.That(NetworkMatchController.IsExpectedCohortAdmitted(2, 2), Is.True);
        Assert.That(NetworkMatchController.IsExpectedCohortAdmitted(2, 3), Is.True);
    }

    [Test]
    public void FrozenClient_WaitsForMissingOrClosedHostSession()
    {
        var host = new ProfileId("host");
        var client = new ProfileId("client");
        RaidCode.TryParse("123456", out RaidCode code);
        RaidTeamId.TryCreate(1, out RaidTeamId teamId);
        RaidLaunchContext.TryCreate(
            code,
            host,
            new[]
            {
                new RaidLaunchParticipant(host, teamId),
                new RaidLaunchParticipant(client, teamId)
            },
            client,
            1,
            out RaidLaunchContext frozen);

        Assert.That(
            SessionConnectionCoordinator.ShouldRetryRaidSessionAvailability(
                frozen, RaidConnectionRole.Client, ShutdownReason.GameNotFound),
            Is.True);
        Assert.That(
            SessionConnectionCoordinator.ShouldRetryRaidSessionAvailability(
                frozen, RaidConnectionRole.Client, ShutdownReason.GameClosed),
            Is.True);
        Assert.That(
            SessionConnectionCoordinator.ShouldRetryRaidSessionAvailability(
                frozen, RaidConnectionRole.Client, ShutdownReason.Error),
            Is.False);

        // Host role never retries
        Assert.That(
            SessionConnectionCoordinator.ShouldRetryRaidSessionAvailability(
                frozen, RaidConnectionRole.Host, ShutdownReason.GameClosed),
            Is.False);
        Assert.That(
            SessionConnectionCoordinator.ShouldRetryRaidSessionAvailability(
                frozen, RaidConnectionRole.Host, ShutdownReason.GameNotFound),
            Is.False);

        // Null launch context (manual join) never retries
        Assert.That(
            SessionConnectionCoordinator.ShouldRetryRaidSessionAvailability(
                null, RaidConnectionRole.Client, ShutdownReason.GameClosed),
            Is.False);

        // Timeout budget enforcement
        Assert.That(
            SessionConnectionCoordinator.ShouldRetryRaidSessionAvailability(
                frozen, RaidConnectionRole.Client, ShutdownReason.GameClosed, remainingSeconds: 15f),
            Is.True);
        Assert.That(
            SessionConnectionCoordinator.ShouldRetryRaidSessionAvailability(
                frozen, RaidConnectionRole.Client, ShutdownReason.GameClosed, remainingSeconds: 0f),
            Is.False);
        Assert.That(
            SessionConnectionCoordinator.ShouldRetryRaidSessionAvailability(
                frozen, RaidConnectionRole.Client, ShutdownReason.GameClosed, remainingSeconds: -5f),
            Is.False);
    }
}
#endif
