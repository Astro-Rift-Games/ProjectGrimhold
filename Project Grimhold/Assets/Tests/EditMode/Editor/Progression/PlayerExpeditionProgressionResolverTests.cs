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
    }
}
#endif
