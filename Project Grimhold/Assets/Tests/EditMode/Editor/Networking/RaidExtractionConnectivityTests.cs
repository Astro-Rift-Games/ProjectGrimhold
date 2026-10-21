#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace Tests.EditMode.Networking
{
    [TestFixture]
    public sealed class RaidExtractionConnectivityTests
    {
        private const string TeamCoordinatorPath =
            "Assets/Scripts/Scenario/Extraction/TeamExtractionCoordinator.cs";
        private const string SpawnManagerPath =
            "Assets/Scripts/Networking/NetworkSpawnManager.cs";

        [Test]
        public void CancellationReason_ContainsRequiredMemberDisconnected()
        {
            bool hasDisconnectedReason = Enum.IsDefined(
                typeof(TeamExtractionCancelReason),
                "RequiredMemberDisconnected");
            Assert.That(hasDisconnectedReason, Is.True, "TeamExtractionCancelReason must define RequiredMemberDisconnected.");
        }

        [Test]
        public void TeamExtractionCoordinator_ChecksParticipantConnectivity()
        {
            string source = File.ReadAllText(TeamCoordinatorPath);
            int populateIndex = source.IndexOf("private bool TryBuildMemberSnapshots", StringComparison.Ordinal);
            Assert.That(populateIndex, Is.GreaterThanOrEqualTo(0));

            int nextMethodIndex = source.IndexOf("private void StartTeamCountdown", populateIndex, StringComparison.Ordinal);
            Assert.That(nextMethodIndex, Is.GreaterThan(populateIndex));

            string methodBody = source.Substring(populateIndex, nextMethodIndex - populateIndex);
            Assert.That(methodBody, Does.Contain("RaidParticipantConnectivity"));
            Assert.That(methodBody, Does.Contain("!connectivity.IsDisconnected"));
        }

        [Test]
        public void EvaluateContinuation_CancelsOnParticipantDisconnected()
        {
            RaidParticipantId.TryCreate(1, out RaidParticipantId p1);
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(p1, RaidParticipantState.Raiding, isConnected: false, isInsideExact: true, isInsideTolerant: true)
            };
            var signature = TeamExtractionRequirementRules.ComputeSignature(members);

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateContinuation(
                isRitualCompleted: true,
                activeSignature: signature,
                members: members);

            Assert.That(result.ShouldContinue, Is.False);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.RequiredMemberDisconnected));
        }

        [Test]
        public void FallbackWithoutDecider_RemovesParticipantFromDisconnectedRegistryAndUpdatesGate()
        {
            string source = File.ReadAllText(SpawnManagerPath);
            int fallbackIndex = source.IndexOf("private void ReevaluateRetainedParticipantsFallback", StringComparison.Ordinal);
            Assert.That(fallbackIndex, Is.GreaterThanOrEqualTo(0));

            int nextMethodIndex = source.IndexOf("private static bool IsAvatarDowned", fallbackIndex, StringComparison.Ordinal);
            Assert.That(nextMethodIndex, Is.GreaterThan(fallbackIndex));

            string methodBody = source.Substring(fallbackIndex, nextMethodIndex - fallbackIndex);
            Assert.That(methodBody, Does.Contain("_disconnectedRegistry.Remove"));
            Assert.That(methodBody, Does.Contain("UpdateMatchSessionGate"));
        }
    }
}
#endif
