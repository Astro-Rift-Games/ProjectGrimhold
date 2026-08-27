#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class PlayerExpeditionProgressionResolverTests
    {
        [TestCase(1, 0, true)]
        [TestCase(1, 99, true)]
        [TestCase(1, 100, false)]
        [TestCase(0, 0, false)]
        [TestCase(30, 0, true)]
        [TestCase(30, 1, false)]
        [TestCase(31, 0, false)]
        [TestCase(1, -1, false)]
        public void Baseline_UsesTheCompleteInitialCurveState(
            int level,
            long experience,
            bool expected)
        {
            Assert.That(
                PlayerExpeditionProgressionResolver.IsValidBaseline(level, experience),
                Is.EqualTo(expected));
        }

        [Test]
        public void IntegrationResult_PreservesUnderlyingDomainFailures()
        {
            PlayerExpeditionProgressionFinalizationResult resolution =
                PlayerExpeditionProgressionFinalizationResult.FromResolutionFailure(
                    ExpeditionExperienceResolutionFailure.InvalidSnapshot);
            PlayerExpeditionProgressionFinalizationResult application =
                PlayerExpeditionProgressionFinalizationResult.FromApplicationFailure(
                    ConsolidatedExperienceApplicationFailure.InvalidProgressionState);

            Assert.That(
                resolution.Status,
                Is.EqualTo(PlayerExpeditionProgressionFinalizationStatus.ResolutionFailed));
            Assert.That(
                resolution.ResolutionFailure,
                Is.EqualTo(ExpeditionExperienceResolutionFailure.InvalidSnapshot));
            Assert.That(
                application.Status,
                Is.EqualTo(PlayerExpeditionProgressionFinalizationStatus.ApplicationFailed));
            Assert.That(
                application.ApplicationFailure,
                Is.EqualTo(ConsolidatedExperienceApplicationFailure.InvalidProgressionState));
        }

        [Test]
        public void ProgressionResult_IsImmutableValueProjectionOfCommittedFacts()
        {
            var snapshot = new ExpeditionExperienceSnapshot(10, 4, 5, 7);
            var resolution = new ExpeditionExperienceResolution(
                ExpeditionExperienceResolutionOutcome.Extracted,
                snapshot,
                10_000,
                26);
            var application = new ExperienceApplicationResult(1, 90, 2, 16, 1);
            var first = new ExpeditionProgressionResult(
                resolution,
                application,
                1,
                2,
                3,
                4,
                5,
                70,
                105,
                false);
            var same = new ExpeditionProgressionResult(
                resolution,
                application,
                1,
                2,
                3,
                4,
                5,
                70,
                105,
                false);
            var different = new ExpeditionProgressionResult(
                resolution,
                application,
                2,
                2,
                3,
                4,
                5,
                70,
                105,
                false);

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(different));
            Assert.That(first.CombatExperience, Is.EqualTo(14));
            Assert.That(first.ProvisionalExperienceTotal, Is.EqualTo(26));
            Assert.That(first.NextLevelExperienceRequirement, Is.EqualTo(105));
            Assert.That(first.IsMaxLevel, Is.False);
        }

        [Test]
        public void CommittedLevelProgress_RequiresAnUnambiguousMaximumState()
        {
            var normal = new ExperienceApplicationResult(1, 0, 2, 5, 1);
            Assert.That(
                PlayerExpeditionProgressionResolver.TryResolveCommittedLevelProgress(
                    normal,
                    out bool normalIsMax,
                    out long normalRequirement),
                Is.True);
            Assert.That(normalIsMax, Is.False);
            Assert.That(normalRequirement, Is.GreaterThan(0));

            var maximum = new ExperienceApplicationResult(29, 0, 30, 0, 1);
            Assert.That(
                PlayerExpeditionProgressionResolver.TryResolveCommittedLevelProgress(
                    maximum,
                    out bool maximumIsMax,
                    out long maximumRequirement),
                Is.True);
            Assert.That(maximumIsMax, Is.True);
            Assert.That(maximumRequirement, Is.Zero);

            var invalidMaximum = new ExperienceApplicationResult(29, 0, 30, 1, 1);
            Assert.That(
                PlayerExpeditionProgressionResolver.TryResolveCommittedLevelProgress(
                    invalidMaximum,
                    out _,
                    out _),
                Is.False);
        }
    }
}
#endif
