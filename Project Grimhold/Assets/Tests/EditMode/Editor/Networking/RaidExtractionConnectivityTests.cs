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
        public void CancellationReason_ReportsRequiredMemberDisconnected()
        {
            TeamMemberExtractionSnapshot disconnected = Member(1, isConnected: false);
            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true, new[] { disconnected });

            Assert.That(disconnected.IsRequiredMember, Is.True,
                "Disconnection must not silently remove a recoverable member from the team.");
            Assert.That(result.CanStart, Is.False);
            Assert.That(result.ShouldContinue, Is.False);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.RequiredMemberDisconnected));
            Assert.That(result.RequiredMemberCount, Is.EqualTo(1));
            Assert.That(result.ConnectedRequiredCount, Is.Zero);
            Assert.That(result.PresentRequiredCount, Is.EqualTo(1));
        }

        [Test]
        public void TeamExtractionStart_ChecksParticipantConnectivity()
        {
            var members = new[] { Member(1, isConnected: true), Member(2, isConnected: true) };
            TeamExtractionEvaluationResult connected = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true, members);

            Assert.That(connected.CanStart, Is.True);
            Assert.That(connected.CancelReason, Is.EqualTo(TeamExtractionCancelReason.None));

            members[1] = Member(2, isConnected: false);
            TeamExtractionEvaluationResult disconnected = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true, members);

            Assert.That(disconnected.CanStart, Is.False);
            Assert.That(disconnected.ShouldContinue, Is.False);
            Assert.That(disconnected.CancelReason, Is.EqualTo(TeamExtractionCancelReason.RequiredMemberDisconnected));
            Assert.That(disconnected.RequiredMemberCount, Is.EqualTo(2));
            Assert.That(disconnected.ConnectedRequiredCount, Is.EqualTo(1));
            Assert.That(disconnected.PresentRequiredCount, Is.EqualTo(2));
            Assert.That(disconnected.Signature, Is.EqualTo(connected.Signature),
                "A disconnected teammate stays in the required composition until resolved.");
        }

        [Test]
        public void EvaluateContinuation_CancelsOnParticipantDisconnected()
        {
            var members = new[] { Member(1, isConnected: true), Member(2, isConnected: true) };
            TeamExtractionEvaluationResult start = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true, members);
            Assert.That(start.CanStart, Is.True);

            TeamExtractionEvaluationResult connected = TeamExtractionRequirementRules.EvaluateContinuation(
                isRitualCompleted: true, start.Signature, members);
            Assert.That(connected.ShouldContinue, Is.True);

            members[1] = Member(2, isConnected: false);
            TeamExtractionEvaluationResult disconnected = TeamExtractionRequirementRules.EvaluateContinuation(
                isRitualCompleted: true, start.Signature, members);

            Assert.That(disconnected.CanStart, Is.False);
            Assert.That(disconnected.ShouldContinue, Is.False);
            Assert.That(disconnected.CancelReason, Is.EqualTo(TeamExtractionCancelReason.RequiredMemberDisconnected));
            Assert.That(disconnected.RequiredMemberCount, Is.EqualTo(2));
            Assert.That(disconnected.ConnectedRequiredCount, Is.EqualTo(1));
            Assert.That(disconnected.Signature, Is.EqualTo(start.Signature));

            members[1] = Member(2, isConnected: true);
            TeamExtractionEvaluationResult reconnected = TeamExtractionRequirementRules.EvaluateContinuation(
                isRitualCompleted: true, start.Signature, members);
            Assert.That(reconnected.ShouldContinue, Is.True);
            Assert.That(reconnected.CancelReason, Is.EqualTo(TeamExtractionCancelReason.None));
        }

        [Test]
        public void EvaluateContinuation_CancelsOnSingleParticipantDisconnected()
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

        private static TeamMemberExtractionSnapshot Member(int id, bool isConnected)
        {
            Assert.That(RaidParticipantId.TryCreate(id, out RaidParticipantId participantId), Is.True);
            return new TeamMemberExtractionSnapshot(
                participantId, RaidParticipantState.Raiding, isConnected,
                isInsideExact: true, isInsideTolerant: true);
        }
    }
}
#endif
