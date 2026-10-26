using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Scenario
{
    public sealed class TeamExtractionProgressTests
    {
        private GameObject _holder;
        private TeamExtractionProgress _progress;
        private ExtractionConfig _config;

        [SetUp]
        public void SetUp()
        {
            _holder = new GameObject("TeamExtractionProgressTests");
            _progress = _holder.AddComponent<TeamExtractionProgress>();
            _progress._allowEditModeSimulation = true;

            _config = ScriptableObject.CreateInstance<ExtractionConfig>();
            _progress.SetConfigForTesting(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Quota_DerivedFromInitialTeamSize_SoloAndDuo()
        {
            RaidTeamId.TryCreate(1, out RaidTeamId soloTeam);
            RaidTeamId.TryCreate(2, out RaidTeamId duoTeam);

            Assert.That(_progress.TryInitializeTeam(soloTeam, 1), Is.True);
            Assert.That(_progress.TryInitializeTeam(duoTeam, 2), Is.True);

            Assert.That(_progress.TryGetProgress(soloTeam, out int soloCurrent, out int soloQuota), Is.True);
            Assert.That(soloCurrent, Is.Zero);
            Assert.That(soloQuota, Is.EqualTo(100)); // Default Solo quota

            Assert.That(_progress.TryGetProgress(duoTeam, out int duoCurrent, out int duoQuota), Is.True);
            Assert.That(duoCurrent, Is.Zero);
            Assert.That(duoQuota, Is.EqualTo(150)); // Default Duo quota
        }

        [Test]
        public void Contribution_AppliesSinglePool_SaturatesAtQuota_AndRequestsAssignmentOnce()
        {
            RaidTeamId.TryCreate(1, out RaidTeamId teamId);
            _progress.TryInitializeTeam(teamId, 1);

            // First contribution
            Assert.That(_progress.TryApplyContribution(teamId, 40, out int next1, out bool completed1), Is.True);
            Assert.That(next1, Is.EqualTo(40));
            Assert.That(completed1, Is.False);

            Assert.That(_progress.TryGetSnapshot(teamId, out ExtractionProgressSnapshot snap1), Is.True);
            Assert.That(snap1.CurrentProgress, Is.EqualTo(40));
            Assert.That(snap1.IsQuotaComplete, Is.False);
            Assert.That(snap1.AssignmentRequested, Is.False);

            // Second contribution exceeding quota saturates at 100
            Assert.That(_progress.TryApplyContribution(teamId, 80, out int next2, out bool completed2), Is.True);
            Assert.That(next2, Is.EqualTo(100));
            Assert.That(completed2, Is.True);

            Assert.That(_progress.TryGetSnapshot(teamId, out ExtractionProgressSnapshot snap2), Is.True);
            Assert.That(snap2.CurrentProgress, Is.EqualTo(100));
            Assert.That(snap2.IsQuotaComplete, Is.True);
            Assert.That(snap2.AssignmentRequested, Is.True);

            // Subsequent contributions after quota completion are rejected
            Assert.That(_progress.TryApplyContribution(teamId, 10, out int next3, out bool completed3), Is.False);
            Assert.That(next3, Is.EqualTo(100));
        }

        [Test]
        public void Contribution_AfterMemberAbsent_DoesNotReduceTeamQuota()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team), Is.True);
            var survivor = new RaidLaunchParticipant(new ProfileId("survivor"), team);
            var partner = new RaidLaunchParticipant(new ProfileId("partner"), team);
            Assert.That(RaidTeamRoster.TryCreate(new[] { survivor, partner }, out RaidTeamRoster initial), Is.True);
            Assert.That(_progress.TryInitializeFromRoster(initial), Is.True);
            Assert.That(_progress.TryApplyContribution(team, 40, out int before, out _), Is.True);
            Assert.That(before, Is.EqualTo(40), "A Duo contribution is not multiplied by two.");

            // A later roster without the partner must not replace the frozen initial composition.
            Assert.That(RaidTeamRoster.TryCreate(new[] { survivor }, out RaidTeamRoster remaining), Is.True);
            Assert.That(_progress.TryInitializeFromRoster(remaining), Is.True);
            Assert.That(_progress.TryGetProgress(team, out int preserved, out int quota), Is.True);
            Assert.That(preserved, Is.EqualTo(40));
            Assert.That(quota, Is.EqualTo(150));
            Assert.That(_progress.TryApplyContribution(team, 10, out int after, out bool completed), Is.True);
            Assert.That(after, Is.EqualTo(50));
            Assert.That(completed, Is.False);
            Assert.That(_progress.TryGetSnapshot(team, out ExtractionProgressSnapshot snapshot), Is.True);
            Assert.That(snapshot.Quota, Is.EqualTo(150));
            Assert.That(snapshot.AssignmentRequested, Is.False);
        }

        [TestCase(40, false)]
        [TestCase(150, true)]
        public void CopyStateFrom_FreshInstance_PreservesProgressQuotaAndAssignmentAfterInitialization(
            int contribution, bool assignmentRequested)
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team), Is.True);
            Assert.That(_progress.TryInitializeTeam(team, 2), Is.True);
            Assert.That(_progress.TryApplyContribution(team, contribution, out _, out _), Is.True);
            var restoredHolder = new GameObject("RestoredTeamProgress");
            try
            {
                var restored = restoredHolder.AddComponent<TeamExtractionProgress>();
                restored.CopyStateFrom(_progress);
                var survivor = new RaidLaunchParticipant(new ProfileId("survivor"), team);
                Assert.That(RaidTeamRoster.TryCreate(new[] { survivor }, out RaidTeamRoster roster), Is.True);
                Assert.That(restored.TryInitializeFromRoster(roster), Is.True);
                Assert.That(restored.TryInitializeTeam(team, 1), Is.True, "Standalone fallback is idempotent too.");
                Assert.That(restored.TryGetSnapshot(team, out ExtractionProgressSnapshot snapshot), Is.True);
                Assert.That(snapshot.CurrentProgress, Is.EqualTo(contribution));
                Assert.That(snapshot.Quota, Is.EqualTo(150));
                Assert.That(snapshot.AssignmentRequested, Is.EqualTo(assignmentRequested));
            }
            finally
            {
                Object.DestroyImmediate(restoredHolder);
            }
        }

        [Test]
        public void MultipleTeams_AreCompletelyIsolatedAndIndependent()
        {
            RaidTeamId.TryCreate(1, out RaidTeamId team1);
            RaidTeamId.TryCreate(2, out RaidTeamId team2);

            _progress.TryInitializeTeam(team1, 1);
            _progress.TryInitializeTeam(team2, 2);

            // Apply 50 to Team 1
            Assert.That(_progress.TryApplyContribution(team1, 50, out _, out _), Is.True);

            // Team 1 is at 50/100, Team 2 is still at 0/150
            Assert.That(_progress.TryGetSnapshot(team1, out ExtractionProgressSnapshot snap1), Is.True);
            Assert.That(snap1.CurrentProgress, Is.EqualTo(50));
            Assert.That(snap1.Quota, Is.EqualTo(100));

            Assert.That(_progress.TryGetSnapshot(team2, out ExtractionProgressSnapshot snap2), Is.True);
            Assert.That(snap2.CurrentProgress, Is.Zero);
            Assert.That(snap2.Quota, Is.EqualTo(150));

            // Complete Team 1
            Assert.That(_progress.TryApplyContribution(team1, 50, out _, out bool completed1), Is.True);
            Assert.That(completed1, Is.True);

            Assert.That(_progress.TryGetSnapshot(team1, out snap1), Is.True);
            Assert.That(snap1.AssignmentRequested, Is.True);

            // Team 2 assignment request remains false
            Assert.That(_progress.TryGetSnapshot(team2, out snap2), Is.True);
            Assert.That(snap2.AssignmentRequested, Is.False);
        }

        [Test]
        public void RejectsInvalidContributions_ZeroOrNegativeAmountOrUnassignedTeam()
        {
            RaidTeamId.TryCreate(1, out RaidTeamId teamId);
            RaidTeamId.TryCreate(9, out RaidTeamId unassignedTeam);
            _progress.TryInitializeTeam(teamId, 1);

            // Zero amount
            Assert.That(_progress.TryApplyContribution(teamId, 0, out _, out _), Is.False);

            // Negative amount
            Assert.That(_progress.TryApplyContribution(teamId, -5, out _, out _), Is.False);

            // Unassigned team
            Assert.That(_progress.TryApplyContribution(unassignedTeam, 10, out _, out _), Is.False);
        }

        [Test]
        public void TryInitializeFromRoster_SetsUpAllTeamsInCohort()
        {
            RaidTeamId.TryCreate(1, out RaidTeamId team1);
            RaidTeamId.TryCreate(2, out RaidTeamId team2);

            var participants = new[]
            {
                new RaidLaunchParticipant(new ProfileId("p1"), team1),
                new RaidLaunchParticipant(new ProfileId("p2"), team2),
                new RaidLaunchParticipant(new ProfileId("p3"), team2),
            };

            Assert.That(RaidTeamRoster.TryCreate(participants, out RaidTeamRoster roster), Is.True);
            Assert.That(_progress.TryInitializeFromRoster(roster), Is.True);

            Assert.That(_progress.IsAssignedToTeam(team1), Is.True);
            Assert.That(_progress.IsAssignedToTeam(team2), Is.True);

            Assert.That(_progress.TryGetProgress(team1, out _, out int quota1), Is.True);
            Assert.That(quota1, Is.EqualTo(100)); // Solo

            Assert.That(_progress.TryGetProgress(team2, out _, out int quota2), Is.True);
            Assert.That(quota2, Is.EqualTo(150)); // Duo
        }
    }
}
