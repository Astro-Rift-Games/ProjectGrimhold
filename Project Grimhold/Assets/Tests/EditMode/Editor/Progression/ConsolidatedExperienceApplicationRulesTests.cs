using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class ConsolidatedExperienceApplicationRulesTests
    {
        [Test]
        public void Apply_PositiveExperienceWithoutLeveling()
        {
            ExperienceCurve curve = CreateCurve(100, 200);
            ExpeditionExperienceResolution resolution = CreateResolution(30);

            Assert.That(TryApply(curve, default, 1, 40, resolution, out ConsolidatedExperienceApplication application,
                out ConsolidatedExperienceApplicationFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(ConsolidatedExperienceApplicationFailure.None));
            AssertApplication(application, 1, 40, 1, 70, 0);
        }

        [Test]
        public void Apply_PreservesRemainderAfterLeveling()
        {
            ExperienceCurve curve = CreateCurve(100, 200);
            ExpeditionExperienceResolution resolution = CreateResolution(35);

            Assert.That(TryApply(curve, default, 1, 90, resolution, out ConsolidatedExperienceApplication application,
                out _), Is.True);

            AssertApplication(application, 1, 90, 2, 25, 1);
        }

        [Test]
        public void Apply_OneResolutionCanGainMultipleLevels()
        {
            ExperienceCurve curve = CreateCurve(100, 200, 300);
            ExpeditionExperienceResolution resolution = CreateResolution(250);

            Assert.That(TryApply(curve, default, 1, 90, resolution, out ConsolidatedExperienceApplication application,
                out _), Is.True);

            AssertApplication(application, 1, 90, 3, 40, 2);
        }

        [Test]
        public void Apply_DiscardsRemainderWhenMaximumLevelIsReached()
        {
            ExperienceCurve curve = CreateCurve(10, 20);
            ExpeditionExperienceResolution resolution = CreateResolution(long.MaxValue);

            Assert.That(TryApply(curve, default, 1, 0, resolution, out ConsolidatedExperienceApplication application,
                out _), Is.True);

            AssertApplication(application, 1, 0, 3, 0, 2);
        }

        [Test]
        public void Apply_PositiveExperienceAtMaximumLevelIsConsumedWithoutChanges()
        {
            ExperienceCurve curve = CreateCurve(10, 20);
            ExpeditionExperienceResolution resolution = CreateResolution(50);

            Assert.That(TryApply(curve, default, 3, 0, resolution, out ConsolidatedExperienceApplication application,
                out _), Is.True);

            AssertApplication(application, 3, 0, 3, 0, 0);
        }

        [Test]
        public void Apply_ResolvedZeroExperienceIsConsumedAsNoOp()
        {
            ExperienceCurve curve = CreateCurve(100, 200);
            ExpeditionExperienceResolution resolution = CreateResolution(0);

            Assert.That(TryApply(curve, default, 2, 45, resolution, out ConsolidatedExperienceApplication application,
                out _), Is.True);

            AssertApplication(application, 2, 45, 2, 45, 0);
        }

        [Test]
        public void Apply_SecondAttemptRejectsAndPreservesFirstResultBeforeOtherValidation()
        {
            ExperienceCurve curve = CreateCurve(100, 200);
            ExpeditionExperienceResolution resolution = CreateResolution(30);
            Assert.That(TryApply(curve, default, 1, 40, resolution, out ConsolidatedExperienceApplication first,
                out _), Is.True);

            Assert.That(TryApply(null, first, 0, -1, default, out ConsolidatedExperienceApplication candidate,
                out ConsolidatedExperienceApplicationFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(ConsolidatedExperienceApplicationFailure.AlreadyApplied));
            AssertApplication(candidate, 1, 40, 1, 70, 0);
        }

        [Test]
        public void Apply_UnresolvedResolutionRejectsWithoutConsumingApplication()
        {
            ExperienceCurve curve = CreateCurve(100, 200);

            Assert.That(TryApply(curve, default, 1, 0, default, out ConsolidatedExperienceApplication candidate,
                out ConsolidatedExperienceApplicationFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(ConsolidatedExperienceApplicationFailure.UnresolvedResolution));
            AssertPending(candidate);
        }

        [TestCase(0, 0)]
        [TestCase(4, 0)]
        [TestCase(1, -1)]
        [TestCase(1, 100)]
        [TestCase(3, 1)]
        public void Apply_InvalidProgressionStateRejectsWithoutConsumingApplication(
            int currentLevel,
            long currentExperience)
        {
            ExperienceCurve curve = CreateCurve(100, 200);
            ExpeditionExperienceResolution resolution = CreateResolution(10);

            Assert.That(TryApply(curve, default, currentLevel, currentExperience, resolution,
                out ConsolidatedExperienceApplication candidate,
                out ConsolidatedExperienceApplicationFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(ConsolidatedExperienceApplicationFailure.InvalidProgressionState));
            AssertPending(candidate);
        }

        [Test]
        public void Apply_NullCurveRejectsWithoutConsumingApplication()
        {
            ExpeditionExperienceResolution resolution = CreateResolution(10);

            Assert.That(TryApply(null, default, 1, 0, resolution, out ConsolidatedExperienceApplication candidate,
                out ConsolidatedExperienceApplicationFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(ConsolidatedExperienceApplicationFailure.InvalidProgressionState));
            AssertPending(candidate);
        }

        [Test]
        public void Apply_IsDeterministicForIdenticalInputs()
        {
            ExperienceCurve curve = CreateCurve(100, 200, 300);
            ExpeditionExperienceResolution resolution = CreateResolution(275);

            Assert.That(TryApply(curve, default, 2, 50, resolution, out ConsolidatedExperienceApplication first,
                out ConsolidatedExperienceApplicationFailure firstFailure), Is.True);
            Assert.That(TryApply(curve, default, 2, 50, resolution, out ConsolidatedExperienceApplication second,
                out ConsolidatedExperienceApplicationFailure secondFailure), Is.True);

            Assert.That(secondFailure, Is.EqualTo(firstFailure));
            AssertApplication(second,
                first.Result.PreviousLevel,
                first.Result.PreviousExperience,
                first.Result.ResultingLevel,
                first.Result.ResultingExperience,
                first.Result.LevelsGained);
        }

        [Test]
        public void Apply_LongMaxValueReachesInitialCurveMaximumWithoutOverflow()
        {
            ExpeditionExperienceResolution resolution = CreateResolution(long.MaxValue);

            Assert.That(TryApply(
                ProgressionBalanceDefaults.InitialExperienceCurve,
                default,
                1,
                0,
                resolution,
                out ConsolidatedExperienceApplication application,
                out _), Is.True);

            AssertApplication(application, 1, 0, 30, 0, 29);
        }

        private static bool TryApply(
            ExperienceCurve curve,
            in ConsolidatedExperienceApplication previous,
            int currentLevel,
            long currentExperience,
            in ExpeditionExperienceResolution resolution,
            out ConsolidatedExperienceApplication candidate,
            out ConsolidatedExperienceApplicationFailure failure) =>
            ConsolidatedExperienceApplicationRules.TryApply(
                curve,
                previous,
                currentLevel,
                currentExperience,
                resolution,
                out candidate,
                out failure);

        private static ExperienceCurve CreateCurve(params long[] requirements)
        {
            Assert.That(ExperienceCurve.TryCreate(requirements, out ExperienceCurve curve), Is.True);
            return curve;
        }

        private static ExpeditionExperienceResolution CreateResolution(long consolidatedExperience)
        {
            ExpeditionExperienceSnapshot snapshot = ExpeditionExperienceSnapshot.Empty;
            if (consolidatedExperience > 0)
            {
                Assert.That(ExpeditionExperienceRules.TryApplyNormalReward(
                    snapshot,
                    ExpeditionExperienceCategory.Kill,
                    consolidatedExperience,
                    out snapshot,
                    out ExpeditionExperienceApplicationFailure applicationFailure), Is.True);
                Assert.That(applicationFailure, Is.EqualTo(ExpeditionExperienceApplicationFailure.None));
            }

            Assert.That(ExpeditionExperienceResolutionRules.TryResolve(
                default,
                snapshot,
                ExpeditionExperienceResolutionOutcome.Extracted,
                ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy,
                out ExpeditionExperienceResolution resolution,
                out ExpeditionExperienceResolutionFailure resolutionFailure), Is.True);
            Assert.That(resolutionFailure, Is.EqualTo(ExpeditionExperienceResolutionFailure.None));
            Assert.That(resolution.ConsolidatedExperience, Is.EqualTo(consolidatedExperience));
            return resolution;
        }

        private static void AssertPending(in ConsolidatedExperienceApplication application)
        {
            Assert.That(application.IsApplied, Is.False);
            Assert.That(application.Result, Is.EqualTo(default(ExperienceApplicationResult)));
        }

        private static void AssertApplication(
            in ConsolidatedExperienceApplication application,
            int previousLevel,
            long previousExperience,
            int resultingLevel,
            long resultingExperience,
            int levelsGained)
        {
            Assert.That(application.IsApplied, Is.True);
            Assert.That(application.Result.PreviousLevel, Is.EqualTo(previousLevel));
            Assert.That(application.Result.PreviousExperience, Is.EqualTo(previousExperience));
            Assert.That(application.Result.ResultingLevel, Is.EqualTo(resultingLevel));
            Assert.That(application.Result.ResultingExperience, Is.EqualTo(resultingExperience));
            Assert.That(application.Result.LevelsGained, Is.EqualTo(levelsGained));
        }
    }
}
