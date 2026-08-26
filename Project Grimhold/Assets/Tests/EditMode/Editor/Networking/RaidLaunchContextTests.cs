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
        RaidLaunchParticipant[] participants = CreateParticipants(profiles);

        Assert.That(
            RaidLaunchContext.TryCreate(code, profiles[0], participants, profiles[0], 9, out RaidLaunchContext context),
            Is.True);
        Assert.That(context.Participants, Is.EqualTo(participants));
        Assert.That(context.ParticipantProfileIds, Is.EqualTo(profiles));
        Assert.That(context.LaunchRevision, Is.EqualTo(9));

        participants[0] = default;
        Assert.That(context.Participants[0].ProfileId, Is.EqualTo(profiles[0]));
        Assert.That(context.Participants[0].TeamId.IsValid, Is.True);
    }

    [Test]
    public void TryCreate_RejectsSeventeenMissingHostMissingLocalAndInvalidRevision()
    {
        RaidCode.TryParse("123456", out RaidCode code);
        ProfileId[] seventeen = CreateProfiles(17);
        ProfileId[] valid = CreateProfiles(2);
        RaidLaunchParticipant[] seventeenParticipants = CreateParticipants(seventeen);
        RaidLaunchParticipant[] validParticipants = CreateParticipants(valid);

        Assert.That(RaidLaunchContext.TryCreate(code, seventeen[0], seventeenParticipants, seventeen[0], 1, out _), Is.False);
        Assert.That(RaidLaunchContext.TryCreate(code, new ProfileId("missing"), validParticipants, valid[0], 1, out _), Is.False);
        Assert.That(RaidLaunchContext.TryCreate(code, valid[0], validParticipants, new ProfileId("missing"), 1, out _), Is.False);
        Assert.That(RaidLaunchContext.TryCreate(code, valid[0], validParticipants, valid[0], 0, out _), Is.False);

        var invalidAffiliation = new[]
        {
            new RaidLaunchParticipant(valid[0], default),
            validParticipants[1]
        };
        Assert.That(RaidLaunchContext.TryCreate(code, valid[0], invalidAffiliation, valid[0], 1, out _), Is.False);

        var duplicateProfile = new[]
        {
            validParticipants[0],
            new RaidLaunchParticipant(valid[0], validParticipants[1].TeamId)
        };
        Assert.That(RaidLaunchContext.TryCreate(code, valid[0], duplicateProfile, valid[0], 1, out _), Is.False);
    }

    [Test]
    public void TransitionTicket_PreservesCanonicalContextWithoutAlternateRoster()
    {
        RaidCode.TryParse("123456", out RaidCode code);
        ProfileId[] profiles = CreateProfiles(2);
        RaidLaunchContext.TryCreate(code, profiles[0], CreateParticipants(profiles), profiles[0], 4, out RaidLaunchContext context);
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
        RaidLaunchContext.TryCreate(
            code,
            host,
            CreateParticipants(new[] { host, clientA }),
            host,
            4,
            out RaidLaunchContext context);
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
        RaidLaunchContext.TryCreate(
            code,
            host,
            CreateParticipants(new[] { host }),
            host,
            4,
            out RaidLaunchContext context);
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

    private static RaidLaunchParticipant[] CreateParticipants(ProfileId[] profiles)
    {
        RaidTeamId.TryCreate(1, out RaidTeamId teamId);
        var participants = new RaidLaunchParticipant[profiles.Length];
        for (int index = 0; index < profiles.Length; index++)
        {
            participants[index] = new RaidLaunchParticipant(profiles[index], teamId);
        }

        return participants;
    }
}
