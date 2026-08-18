using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class RaidLaunchContextTests
{
    [TestCase(1)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(15)]
    [TestCase(16)]
    public void TryCreate_AcceptsSupportedCohortAndPreservesRevision(int count)
    {
        RaidCode.TryParse("123456", out RaidCode code);
        ProfileId[] profiles = CreateProfiles(count);

        Assert.That(
            RaidLaunchContext.TryCreate(code, profiles[0], profiles, profiles[0], 9, out RaidLaunchContext context),
            Is.True);
        Assert.That(context.ParticipantProfileIds, Is.EqualTo(profiles));
        Assert.That(context.LaunchRevision, Is.EqualTo(9));
    }

    [Test]
    public void TryCreate_RejectsSeventeenMissingHostMissingLocalAndInvalidRevision()
    {
        RaidCode.TryParse("123456", out RaidCode code);
        ProfileId[] seventeen = CreateProfiles(17);
        ProfileId[] valid = CreateProfiles(2);

        Assert.That(RaidLaunchContext.TryCreate(code, seventeen[0], seventeen, seventeen[0], 1, out _), Is.False);
        Assert.That(RaidLaunchContext.TryCreate(code, new ProfileId("missing"), valid, valid[0], 1, out _), Is.False);
        Assert.That(RaidLaunchContext.TryCreate(code, valid[0], valid, new ProfileId("missing"), 1, out _), Is.False);
        Assert.That(RaidLaunchContext.TryCreate(code, valid[0], valid, valid[0], 0, out _), Is.False);
    }

    [Test]
    public void TransitionTicket_PreservesCanonicalContextWithoutAlternateRoster()
    {
        RaidCode.TryParse("123456", out RaidCode code);
        ProfileId[] profiles = CreateProfiles(2);
        RaidLaunchContext.TryCreate(code, profiles[0], profiles, profiles[0], 4, out RaidLaunchContext context);
        var reservation = new PendingLoadoutReservation("reservation", new List<StashItem>());
        var ticket = new RaidTransitionTicket(
            new RaidConnectionRequest(code, RaidConnectionRole.Host),
            reservation,
            SessionConnectionState.Town,
            context);

        RaidTransitionTicket updated = ticket.WithState(SessionConnectionState.ConnectingRaid);

        Assert.That(ticket.IsValid, Is.True);
        Assert.That(ticket.LaunchContext, Is.SameAs(context));
        Assert.That(updated.LaunchContext, Is.SameAs(context));
        Assert.That(updated.LaunchContext.ParticipantProfileIds, Is.EqualTo(profiles));
    }

    [Test]
    public void TransitionTicket_RejectsRoleDifferentFromCanonicalContext()
    {
        RaidCode.TryParse("123456", out RaidCode code);
        var host = new ProfileId("host");
        var clientA = new ProfileId("client-a");
        RaidLaunchContext.TryCreate(code, host, new[] { host, clientA }, host, 4, out RaidLaunchContext context);
        var ticket = new RaidTransitionTicket(
            new RaidConnectionRequest(code, RaidConnectionRole.Client),
            new PendingLoadoutReservation("reservation", new List<StashItem>()),
            SessionConnectionState.Town,
            context);

        Assert.That(ticket.IsValid, Is.False);
    }

    [Test]
    public void Release_ConsumesOnlyMatchingRaidCodeRevisionAndLocalProfile()
    {
        RaidCode.TryParse("123456", out RaidCode code);
        var host = new ProfileId("host");
        RaidLaunchContext.TryCreate(code, host, new[] { host }, host, 4, out RaidLaunchContext context);
        var ticket = new RaidTransitionTicket(
            new RaidConnectionRequest(code, RaidConnectionRole.Host),
            new PendingLoadoutReservation("reservation", new List<StashItem>()),
            SessionConnectionState.Town,
            context);

        Assert.That(SessionConnectionCoordinator.CanConsumeLaunchRelease(ticket, "123456", 4, host), Is.True);
        Assert.That(SessionConnectionCoordinator.CanConsumeLaunchRelease(ticket, "123456", 5, host), Is.False);
        Assert.That(SessionConnectionCoordinator.CanConsumeLaunchRelease(ticket, "654321", 4, host), Is.False);
        Assert.That(SessionConnectionCoordinator.CanConsumeLaunchRelease(ticket, "123456", 4, new ProfileId("other")), Is.False);
    }

    private static ProfileId[] CreateProfiles(int count)
    {
        var profiles = new ProfileId[count];
        for (int index = 0; index < count; index++)
        {
            profiles[index] = new ProfileId($"profile-{index}");
        }

        return profiles;
    }
}
