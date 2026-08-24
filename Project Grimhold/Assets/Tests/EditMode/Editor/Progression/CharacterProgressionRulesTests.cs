using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class CharacterProgressionRulesTests
    {
        [Test]
        public void Curve_RejectsNullEmptyAndNonPositiveRequirements()
        {
            Assert.That(ExperienceCurve.TryCreate(null, out _), Is.False);
            Assert.That(ExperienceCurve.TryCreate(new long[0], out _), Is.False);
            Assert.That(ExperienceCurve.TryCreate(new long[] { 10, 0 }, out _), Is.False);
            Assert.That(ExperienceCurve.TryCreate(new long[] { 10, -1 }, out _), Is.False);
        }

        [Test]
        public void Curve_AcceptsAnyPositiveFirstRequirementAndDerivesMaximumLevel()
        {
            Assert.That(ExperienceCurve.TryCreate(new long[] { 7, 11, 13 }, out ExperienceCurve curve), Is.True);

            Assert.That(curve.MaximumLevel, Is.EqualTo(4));
            Assert.That(curve.TryGetRequiredExperience(1, out long first), Is.True);
            Assert.That(first, Is.EqualTo(7));
            Assert.That(curve.TryGetRequiredExperience(curve.MaximumLevel, out long missing), Is.False);
            Assert.That(missing, Is.Zero);
        }

        [Test]
        public void Curve_DefensivelyCopiesRequirements()
        {
            long[] source = { 10, 20 };
            ExperienceCurve.TryCreate(source, out ExperienceCurve curve);

            source[0] = 999;

            Assert.That(curve.TryGetRequiredExperience(1, out long requirement), Is.True);
            Assert.That(requirement, Is.EqualTo(10));
        }

        [Test]
        public void InitialCurve_ContainsDocumentedBalanceAndDerivesLevelThirty()
        {
            long[] expected =
            {
                100, 105, 110, 115, 120, 126, 132, 138, 144, 151,
                158, 165, 173, 181, 190, 199, 208, 218, 228, 239,
                250, 262, 275, 288, 302, 317, 332, 348, 365
            };
            ExperienceCurve curve = ProgressionBalanceDefaults.InitialExperienceCurve;

            Assert.That(curve.MaximumLevel, Is.EqualTo(30));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(curve.TryGetRequiredExperience(index + 1, out long requirement), Is.True);
                Assert.That(requirement, Is.EqualTo(expected[index]));
            }
        }

        [Test]
        public void Apply_AccumulatesExperienceWithoutLeveling()
        {
            ExperienceCurve curve = CreateCurve(100, 200);

            Assert.That(CharacterProgressionRules.TryApplyExperience(curve, 1, 40, 30, out ExperienceApplicationResult result), Is.True);
            AssertResult(result, 1, 40, 1, 70, 0);
        }

        [Test]
        public void Apply_ExactRequirementLevelsWithNoRemainder()
        {
            ExperienceCurve curve = CreateCurve(100, 200);

            Assert.That(CharacterProgressionRules.TryApplyExperience(curve, 1, 40, 60, out ExperienceApplicationResult result), Is.True);
            AssertResult(result, 1, 40, 2, 0, 1);
        }

        [Test]
        public void Apply_PreservesRemainderAcrossMultipleLevels()
        {
            ExperienceCurve curve = CreateCurve(100, 200, 300);

            Assert.That(CharacterProgressionRules.TryApplyExperience(curve, 1, 90, 250, out ExperienceApplicationResult result), Is.True);
            AssertResult(result, 1, 90, 3, 40, 2);
        }

        [Test]
        public void Apply_AtMaximumLevelSucceedsWithoutChanges()
        {
            ExperienceCurve curve = CreateCurve(10, 20);

            Assert.That(CharacterProgressionRules.TryApplyExperience(curve, 3, 0, long.MaxValue, out ExperienceApplicationResult result), Is.True);
            AssertResult(result, 3, 0, 3, 0, 0);
        }

        [Test]
        public void Apply_DiscardsRemainderWhenMaximumLevelIsReached()
        {
            ExperienceCurve curve = CreateCurve(10, 20);

            Assert.That(CharacterProgressionRules.TryApplyExperience(curve, 1, 0, long.MaxValue, out ExperienceApplicationResult result), Is.True);
            AssertResult(result, 1, 0, 3, 0, 2);
        }

        [Test]
        public void Apply_LongMaxValueReachesInitialCurveMaximumWithoutOverflow()
        {
            ExperienceCurve curve = ProgressionBalanceDefaults.InitialExperienceCurve;

            Assert.That(CharacterProgressionRules.TryApplyExperience(curve, 1, 0, long.MaxValue, out ExperienceApplicationResult result), Is.True);
            AssertResult(result, 1, 0, 30, 0, 29);
        }

        [Test]
        public void Apply_IsDeterministicForIdenticalInputs()
        {
            ExperienceCurve curve = CreateCurve(100, 200, 300);

            CharacterProgressionRules.TryApplyExperience(curve, 2, 50, 275, out ExperienceApplicationResult first);
            CharacterProgressionRules.TryApplyExperience(curve, 2, 50, 275, out ExperienceApplicationResult second);

            AssertResult(second, first.PreviousLevel, first.PreviousExperience,
                first.ResultingLevel, first.ResultingExperience, first.LevelsGained);
        }

        [TestCase(0, 0, 1)]
        [TestCase(4, 0, 1)]
        [TestCase(1, -1, 1)]
        [TestCase(1, 100, 1)]
        [TestCase(1, 101, 1)]
        [TestCase(3, 1, 1)]
        [TestCase(1, 0, 0)]
        [TestCase(1, 0, -1)]
        public void Apply_InvalidStateOrAwardReturnsDefaultResult(
            int currentLevel,
            long currentExperience,
            long awardedExperience)
        {
            ExperienceCurve curve = CreateCurve(100, 200);
            int originalLevel = currentLevel;
            long originalExperience = currentExperience;

            Assert.That(CharacterProgressionRules.TryApplyExperience(
                curve, currentLevel, currentExperience, awardedExperience, out ExperienceApplicationResult result), Is.False);
            Assert.That(result, Is.EqualTo(default(ExperienceApplicationResult)));
            Assert.That(currentLevel, Is.EqualTo(originalLevel));
            Assert.That(currentExperience, Is.EqualTo(originalExperience));
        }

        [Test]
        public void Apply_NullCurveReturnsDefaultResult()
        {
            Assert.That(CharacterProgressionRules.TryApplyExperience(
                null, 1, 0, 1, out ExperienceApplicationResult result), Is.False);
            Assert.That(result, Is.EqualTo(default(ExperienceApplicationResult)));
        }

        private static ExperienceCurve CreateCurve(params long[] requirements)
        {
            Assert.That(ExperienceCurve.TryCreate(requirements, out ExperienceCurve curve), Is.True);
            return curve;
        }

        private static void AssertResult(
            ExperienceApplicationResult result,
            int previousLevel,
            long previousExperience,
            int resultingLevel,
            long resultingExperience,
            int levelsGained)
        {
            Assert.That(result.PreviousLevel, Is.EqualTo(previousLevel));
            Assert.That(result.PreviousExperience, Is.EqualTo(previousExperience));
            Assert.That(result.ResultingLevel, Is.EqualTo(resultingLevel));
            Assert.That(result.ResultingExperience, Is.EqualTo(resultingExperience));
            Assert.That(result.LevelsGained, Is.EqualTo(levelsGained));
        }
    }
}
