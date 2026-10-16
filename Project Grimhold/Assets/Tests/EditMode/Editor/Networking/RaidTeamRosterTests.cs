using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode.Networking
{
    [TestFixture]
    public sealed class RaidTeamRosterTests
    {
        [Test]
        public void TryCreate_ValidSoloAndDuoTeams_SucceedsWithExpectedMembers()
        {
            var p1 = new ProfileId("profile-1");
            var p2 = new ProfileId("profile-2");
            var p3 = new ProfileId("profile-3");

            RaidTeamId.TryCreate(1, out RaidTeamId team1);
            RaidTeamId.TryCreate(2, out RaidTeamId team2);

            var participants = new[]
            {
                new RaidLaunchParticipant(p1, team1),
                new RaidLaunchParticipant(p2, team1),
                new RaidLaunchParticipant(p3, team2)
            };

            bool success = RaidTeamRoster.TryCreate(participants, 4, out RaidTeamRoster roster, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(roster.TeamCount, Is.EqualTo(2));
            Assert.That(roster.ContainsTeam(team1), Is.True);
            Assert.That(roster.ContainsTeam(team2), Is.True);

            Assert.That(roster.GetInitialTeamSize(team1), Is.EqualTo(2));
            Assert.That(roster.GetInitialTeamSize(team2), Is.EqualTo(1));

            Assert.That(roster.TryGetTeam(p1, out RaidTeamId resolvedTeam1), Is.True);
            Assert.That(resolvedTeam1, Is.EqualTo(team1));
            Assert.That(roster.TryGetTeam(p3, out RaidTeamId resolvedTeam2), Is.True);
            Assert.That(resolvedTeam2, Is.EqualTo(team2));

            Assert.That(roster.TryGetTeamMembers(team1, out IReadOnlyList<RaidParticipantId> team1Members), Is.True);
            Assert.That(team1Members.Count, Is.EqualTo(2));
            Assert.That(roster.TryAreInitialTeammates(team1Members[0], team1Members[1], out bool areTeammates), Is.True);
            Assert.That(areTeammates, Is.True);

            Assert.That(roster.TryGetTeamMembers(team2, out IReadOnlyList<RaidParticipantId> team2Members), Is.True);
            Assert.That(team2Members.Count, Is.EqualTo(1));
            Assert.That(roster.TryAreInitialTeammates(team1Members[0], team2Members[0], out bool crossTeamTeammates), Is.True);
            Assert.That(crossTeamTeammates, Is.False);
        }

        [Test]
        public void TryCreate_RejectsTeamWithMoreThanTwoMembers_EnforcesRuleA1()
        {
            var p1 = new ProfileId("profile-1");
            var p2 = new ProfileId("profile-2");
            var p3 = new ProfileId("profile-3");

            RaidTeamId.TryCreate(1, out RaidTeamId team1);

            var participants = new[]
            {
                new RaidLaunchParticipant(p1, team1),
                new RaidLaunchParticipant(p2, team1),
                new RaidLaunchParticipant(p3, team1)
            };

            bool success = RaidTeamRoster.TryCreate(participants, 4, out RaidTeamRoster roster, out string error);

            Assert.That(success, Is.False);
            Assert.That(roster, Is.Null);
            Assert.That(error, Does.Contain("invalid size"));
        }

        [Test]
        public void TryCreate_RejectsTeamCountExceedingMaxTeamsPerRaid_EnforcesRuleA2()
        {
            var p1 = new ProfileId("profile-1");
            var p2 = new ProfileId("profile-2");
            var p3 = new ProfileId("profile-3");

            RaidTeamId.TryCreate(1, out RaidTeamId team1);
            RaidTeamId.TryCreate(2, out RaidTeamId team2);
            RaidTeamId.TryCreate(3, out RaidTeamId team3);

            var participants = new[]
            {
                new RaidLaunchParticipant(p1, team1),
                new RaidLaunchParticipant(p2, team2),
                new RaidLaunchParticipant(p3, team3)
            };

            // Max allowed is 2, but we have 3 teams
            bool success = RaidTeamRoster.TryCreate(participants, 2, out RaidTeamRoster roster, out string error);

            Assert.That(success, Is.False);
            Assert.That(roster, Is.Null);
            Assert.That(error, Does.Contain("exceeds maximum allowed teams"));
        }

        [Test]
        public void TryCreate_RejectsDuplicateProfiles()
        {
            var p1 = new ProfileId("profile-1");
            RaidTeamId.TryCreate(1, out RaidTeamId team1);

            var participants = new[]
            {
                new RaidLaunchParticipant(p1, team1),
                new RaidLaunchParticipant(p1, team1)
            };

            bool success = RaidTeamRoster.TryCreate(participants, 4, out RaidTeamRoster roster, out string error);

            Assert.That(success, Is.False);
            Assert.That(roster, Is.Null);
            Assert.That(error, Does.Contain("Duplicate profile"));
        }

        [Test]
        public void TryCreate_RejectsEmptyOrNullCohort()
        {
            bool successNull = RaidTeamRoster.TryCreate(null, 4, out _, out string errorNull);
            Assert.That(successNull, Is.False);
            Assert.That(errorNull, Is.Not.Null);

            bool successEmpty = RaidTeamRoster.TryCreate(Array.Empty<RaidLaunchParticipant>(), 4, out _, out string errorEmpty);
            Assert.That(successEmpty, Is.False);
            Assert.That(errorEmpty, Is.Not.Null);
        }

        [Test]
        public void TryCreate_RejectsInvalidMaxTeamsConfiguration()
        {
            var p1 = new ProfileId("profile-1");
            RaidTeamId.TryCreate(1, out RaidTeamId team1);
            var participants = new[] { new RaidLaunchParticipant(p1, team1) };

            bool successZero = RaidTeamRoster.TryCreate(participants, 0, out _, out string errorZero);
            Assert.That(successZero, Is.False);
            Assert.That(errorZero, Does.Contain("MaxTeamsPerRaid"));

            bool successExceeded = RaidTeamRoster.TryCreate(participants, RaidSessionRules.MaxTeams + 1, out _, out string errorExceeded);
            Assert.That(successExceeded, Is.False);
            Assert.That(errorExceeded, Does.Contain("MaxTeamsPerRaid"));
        }
    }
}
