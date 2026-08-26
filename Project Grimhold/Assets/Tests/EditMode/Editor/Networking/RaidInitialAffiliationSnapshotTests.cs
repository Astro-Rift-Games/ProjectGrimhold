using NUnit.Framework;

public sealed class RaidInitialAffiliationSnapshotTests
{
    [Test]
    public void TeamId_UsesEqualityOnlyAndAcceptsTechnicalRange()
    {
        Assert.That(RaidTeamId.TryCreate(0, out _), Is.False);
        Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId first), Is.True);
        Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId same), Is.True);
        Assert.That(RaidTeamId.TryCreate(16, out RaidTeamId other), Is.True);
        Assert.That(RaidTeamId.TryCreate(17, out _), Is.False);
        Assert.That(first, Is.EqualTo(same));
        Assert.That(first, Is.Not.EqualTo(other));
    }

    [Test]
    public void Snapshot_ReusesCanonicalParticipantAssignmentRegardlessOfPhysicalOrder()
    {
        ProfileId alpha = new("alpha");
        ProfileId middle = new("middle");
        ProfileId omega = new("omega");
        RaidTeamId.TryCreate(1, out RaidTeamId allies);
        RaidTeamId.TryCreate(2, out RaidTeamId enemies);
        RaidLaunchParticipant[] first =
        {
            new(omega, enemies),
            new(alpha, allies),
            new(middle, allies)
        };
        RaidLaunchParticipant[] second =
        {
            new(middle, allies),
            new(omega, enemies),
            new(alpha, allies)
        };

        Assert.That(RaidInitialAffiliationSnapshot.TryCreate(first, out RaidInitialAffiliationSnapshot firstSnapshot), Is.True);
        Assert.That(RaidInitialAffiliationSnapshot.TryCreate(second, out RaidInitialAffiliationSnapshot secondSnapshot), Is.True);

        ProfileId[] firstProfiles = { omega, alpha, middle };
        foreach (RaidLaunchParticipant participant in first)
        {
            Assert.That(
                RaidParticipantIdAssignment.TryResolve(
                    firstProfiles,
                    participant.ProfileId,
                    out RaidParticipantId canonicalId),
                Is.True);
            Assert.That(firstSnapshot.TryGetTeam(canonicalId, out RaidTeamId firstTeam), Is.True);
            Assert.That(secondSnapshot.TryGetTeam(canonicalId, out RaidTeamId secondTeam), Is.True);
            Assert.That(firstTeam, Is.EqualTo(participant.TeamId));
            Assert.That(secondTeam, Is.EqualTo(participant.TeamId));
        }
    }

    [Test]
    public void Snapshot_ResolvesTeammatesAndRejectsIncompleteMembership()
    {
        RaidTeamId.TryCreate(1, out RaidTeamId allies);
        RaidTeamId.TryCreate(2, out RaidTeamId enemies);
        var alpha = new ProfileId("alpha");
        var beta = new ProfileId("beta");
        var gamma = new ProfileId("gamma");
        RaidLaunchParticipant[] participants =
        {
            new(alpha, allies),
            new(beta, allies),
            new(gamma, enemies)
        };

        Assert.That(RaidInitialAffiliationSnapshot.TryCreate(participants, out RaidInitialAffiliationSnapshot snapshot), Is.True);
        ProfileId[] profiles = { alpha, beta, gamma };
        RaidParticipantIdAssignment.TryResolve(profiles, alpha, out RaidParticipantId alphaId);
        RaidParticipantIdAssignment.TryResolve(profiles, beta, out RaidParticipantId betaId);
        RaidParticipantIdAssignment.TryResolve(profiles, gamma, out RaidParticipantId gammaId);

        Assert.That(snapshot.TryAreInitialTeammates(alphaId, betaId, out bool alliesResult), Is.True);
        Assert.That(alliesResult, Is.True);
        Assert.That(snapshot.TryAreInitialTeammates(alphaId, gammaId, out bool enemiesResult), Is.True);
        Assert.That(enemiesResult, Is.False);
        RaidParticipantId.TryCreate(16, out RaidParticipantId missingId);
        Assert.That(snapshot.TryAreInitialTeammates(alphaId, missingId, out _), Is.False);

        Assert.That(
            RaidInitialAffiliationSnapshot.TryCreate(
                new[]
                {
                    new RaidLaunchParticipant(alpha, allies),
                    new RaidLaunchParticipant(alpha, enemies)
                },
                out _),
            Is.False);
        Assert.That(
            RaidInitialAffiliationSnapshot.TryCreate(
                new[] { new RaidLaunchParticipant(alpha, default) },
                out _),
            Is.False);
    }
}
