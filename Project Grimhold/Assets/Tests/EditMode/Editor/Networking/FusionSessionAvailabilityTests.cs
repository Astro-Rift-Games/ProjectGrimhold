#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class FusionSessionAvailabilityTests
{
    [Test]
    public void GameNotFound_WaitsForHost()
    {
        Assert.That(FusionSessionLauncher.IsSessionAvailabilityPending(ShutdownReason.GameNotFound), Is.True);
    }

    [Test]
    public void GameClosed_DoesNotWaitForHost()
    {
        Assert.That(FusionSessionLauncher.IsSessionAvailabilityPending(ShutdownReason.GameClosed), Is.False);
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
    public void CodeAdmission_RemainsOpenOnlyDuringWaiting()
    {
        Assert.That(
            NetworkMatchController.IsCodeAdmissionOpen(true, NetworkMatchController.MatchPhase.WaitingForPlayers),
            Is.True);
        Assert.That(
            NetworkMatchController.IsCodeAdmissionOpen(true, NetworkMatchController.MatchPhase.Starting),
            Is.False);
        Assert.That(
            NetworkMatchController.IsCodeAdmissionOpen(true, NetworkMatchController.MatchPhase.InProgress),
            Is.False);
    }

    [Test]
    public void MissingCodeSession_IsDefinitiveButFrozenClientWaitsForHost()
    {
        var host = new ProfileId("host");
        var frozen = new RaidLaunchManifest(
            "raid", "session", "secret", host, new[] { host }, 1);
        RaidLaunchManifest coded = RaidLaunchManifest.Code.CreateManifest("123456");

        Assert.That(
            SessionConnectionCoordinator.ShouldRetryRaidSessionAvailability(
                frozen, RaidConnectionRole.Client, ShutdownReason.GameNotFound),
            Is.True);
        Assert.That(
            SessionConnectionCoordinator.ShouldRetryRaidSessionAvailability(
                coded, RaidConnectionRole.Client, ShutdownReason.GameNotFound),
            Is.False);
    }
}
#endif
