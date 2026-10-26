using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode.Scenario
{
    [TestFixture]
    public sealed class TeamExtractionRequirementRulesTests
    {
        private static readonly RaidParticipantId P1 = CreateId(1);
        private static readonly RaidParticipantId P2 = CreateId(2);

        private static RaidParticipantId CreateId(int val)
        {
            RaidParticipantId.TryCreate(val, out RaidParticipantId id);
            return id;
        }

        [Test]
        public void EvaluateStart_RitualNotCompleted_BlocksStart()
        {
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true)
            };

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: false,
                members: members);

            Assert.That(result.CanStart, Is.False);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.RitualNotCompleted));
        }

        [Test]
        public void EvaluateStart_Solo_ActiveConnectedInside_CanStart()
        {
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true)
            };

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true,
                members: members);

            Assert.That(result.CanStart, Is.True);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.None));
            Assert.That(result.RequiredMemberCount, Is.EqualTo(1));
            Assert.That(result.ConnectedRequiredCount, Is.EqualTo(1));
            Assert.That(result.PresentRequiredCount, Is.EqualTo(1));
        }

        [Test]
        public void EvaluateStart_Solo_Disconnected_BlocksStart()
        {
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: false, isInsideExact: true, isInsideTolerant: true)
            };

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true,
                members: members);

            Assert.That(result.CanStart, Is.False);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.RequiredMemberDisconnected));
        }

        [Test]
        public void EvaluateStart_Solo_OutsideArea_BlocksStart()
        {
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: false, isInsideTolerant: false)
            };

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true,
                members: members);

            Assert.That(result.CanStart, Is.False);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.RequiredMemberOutsideArea));
        }

        [Test]
        public void EvaluateStart_Solo_DefeatedOrAborted_HasNoRequiredMembers()
        {
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Defeated, isConnected: true, isInsideExact: true, isInsideTolerant: true)
            };

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true,
                members: members);

            Assert.That(result.CanStart, Is.False);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.NoRequiredMembers));
        }

        [Test]
        public void EvaluateStart_Duo_BothActiveConnectedInside_CanStart()
        {
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true),
                new TeamMemberExtractionSnapshot(P2, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true)
            };

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true,
                members: members);

            Assert.That(result.CanStart, Is.True);
            Assert.That(result.RequiredMemberCount, Is.EqualTo(2));
            Assert.That(result.ConnectedRequiredCount, Is.EqualTo(2));
            Assert.That(result.PresentRequiredCount, Is.EqualTo(2));
        }

        [Test]
        public void EvaluateStart_Duo_OneDownedConnectedInside_CanStart_EnforcesRuleD6()
        {
            // Per D-6: Downed connected member inside area is eligible to extract
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true),
                new TeamMemberExtractionSnapshot(P2, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true)
            };

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true,
                members: members);

            Assert.That(result.CanStart, Is.True);
        }

        [Test]
        public void EvaluateStart_Duo_TeammateOutsideArea_BlocksStart()
        {
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true),
                new TeamMemberExtractionSnapshot(P2, RaidParticipantState.Raiding, isConnected: true, isInsideExact: false, isInsideTolerant: false)
            };

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true,
                members: members);

            Assert.That(result.CanStart, Is.False);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.RequiredMemberOutsideArea));
        }

        [Test]
        public void EvaluateStart_Duo_TeammateDisconnected_BlocksStart()
        {
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true),
                new TeamMemberExtractionSnapshot(P2, RaidParticipantState.Raiding, isConnected: false, isInsideExact: true, isInsideTolerant: true)
            };

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true,
                members: members);

            Assert.That(result.CanStart, Is.False);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.RequiredMemberDisconnected));
        }

        [Test]
        public void EvaluateStart_Duo_TeammateDefeated_SurvivorCanExtractSolo()
        {
            // Partner reached Definitive Defeat -> no longer required member
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true),
                new TeamMemberExtractionSnapshot(P2, RaidParticipantState.Defeated, isConnected: false, isInsideExact: false, isInsideTolerant: false)
            };

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateStart(
                isRitualCompleted: true,
                members: members);

            Assert.That(result.CanStart, Is.True);
            Assert.That(result.RequiredMemberCount, Is.EqualTo(1));
        }

        [Test]
        public void EvaluateContinuation_RequiredMembersConnectedInside_DoesNotCancel()
        {
            // Damage, Downed and enemies are deliberately not inputs to this pure API.
            // A Downed avatar remains a Raiding participant; only presence/connectivity matter.
            var initial = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, true, true, true),
                new TeamMemberExtractionSnapshot(P2, RaidParticipantState.Raiding, true, true, true)
            };
            TeamCompositionSignature signature = TeamExtractionRequirementRules.ComputeSignature(initial);
            var current = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, true, true, true),
                new TeamMemberExtractionSnapshot(P2, RaidParticipantState.Raiding, true, false, true)
            };

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateContinuation(
                isRitualCompleted: true, activeSignature: signature, members: current);

            Assert.That(result.ShouldContinue, Is.True);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.None));
            Assert.That(result.Signature, Is.EqualTo(signature));
            Assert.That(result.RequiredMemberCount, Is.EqualTo(2));
            Assert.That(result.ConnectedRequiredCount, Is.EqualTo(2));
            Assert.That(result.PresentRequiredCount, Is.EqualTo(2));
        }

        [Test]
        public void EvaluateContinuation_CompositionChange_TriggersRestart()
        {
            // When countdown started, both P1 and P2 were required
            var startMembers = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true),
                new TeamMemberExtractionSnapshot(P2, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true)
            };
            TeamCompositionSignature originalSignature = TeamExtractionRequirementRules.ComputeSignature(startMembers);

            // Mid-countdown, P2 suffers Definitive Defeat (or "Continue without teammate" confirmed)
            var currentMembers = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true),
                new TeamMemberExtractionSnapshot(P2, RaidParticipantState.Defeated, isConnected: true, isInsideExact: true, isInsideTolerant: true)
            };

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateContinuation(
                isRitualCompleted: true,
                activeSignature: originalSignature,
                members: currentMembers);

            Assert.That(result.ShouldContinue, Is.False);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.CompositionChanged));
        }

        [Test]
        public void EvaluateContinuation_TeammateDisconnects_CancelsCountdown()
        {
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true),
                new TeamMemberExtractionSnapshot(P2, RaidParticipantState.Raiding, isConnected: false, isInsideExact: true, isInsideTolerant: true)
            };
            TeamCompositionSignature signature = TeamExtractionRequirementRules.ComputeSignature(members);

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateContinuation(
                isRitualCompleted: true,
                activeSignature: signature,
                members: members);

            Assert.That(result.ShouldContinue, Is.False);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.RequiredMemberDisconnected));
        }

        [Test]
        public void EvaluateContinuation_TeammateLeavesTolerantArea_CancelsCountdown()
        {
            var members = new[]
            {
                new TeamMemberExtractionSnapshot(P1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true),
                new TeamMemberExtractionSnapshot(P2, RaidParticipantState.Raiding, isConnected: true, isInsideExact: false, isInsideTolerant: false)
            };
            TeamCompositionSignature signature = TeamExtractionRequirementRules.ComputeSignature(members);

            TeamExtractionEvaluationResult result = TeamExtractionRequirementRules.EvaluateContinuation(
                isRitualCompleted: true,
                activeSignature: signature,
                members: members);

            Assert.That(result.ShouldContinue, Is.False);
            Assert.That(result.CancelReason, Is.EqualTo(TeamExtractionCancelReason.RequiredMemberOutsideArea));
        }
    }
}
