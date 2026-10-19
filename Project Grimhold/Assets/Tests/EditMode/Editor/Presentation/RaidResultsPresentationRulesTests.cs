using NUnit.Framework;

namespace Tests.EditMode.Presentation
{
    public sealed class RaidResultsPresentationRulesTests
    {
        [TestCase(RaidParticipantState.Extracted,
            ExpeditionProgressionFinalizationCause.ExtractionConfirmed, RaidResultsOutcome.Extracted)]
        [TestCase(RaidParticipantState.Defeated,
            ExpeditionProgressionFinalizationCause.DefeatConfirmed, RaidResultsOutcome.Defeated)]
        [TestCase(RaidParticipantState.Aborted,
            ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed, RaidResultsOutcome.Abandoned)]
        [TestCase(RaidParticipantState.Aborted,
            ExpeditionProgressionFinalizationCause.None, RaidResultsOutcome.None)]
        [TestCase(RaidParticipantState.Raiding,
            ExpeditionProgressionFinalizationCause.None, RaidResultsOutcome.None)]
        public void Outcome_MapsParticipantResultToHeadline(
            RaidParticipantState state,
            ExpeditionProgressionFinalizationCause cause,
            RaidResultsOutcome expected)
        {
            Assert.That(RaidResultsOutcomeResolver.Resolve(state, cause), Is.EqualTo(expected));
        }

        [TestCase(1, false, true)]
        [TestCase(3, false, true)]
        [TestCase(0, false, false)]
        [TestCase(1, true, false)]
        [TestCase(0, true, false)]
        public void LevelUp_CelebratesOnlyOncePerGainedLevel(
            int levelsGained,
            bool alreadyCelebrated,
            bool expected)
        {
            Assert.That(
                ResultsLevelUpFeedbackPolicy.ShouldCelebrate(levelsGained, alreadyCelebrated),
                Is.EqualTo(expected));
        }
    }
}
