using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class ExpeditionExperienceRulesTests
    {
        [Test]
        public void EmptySnapshot_HasZeroBreakdownAndDerivedTotal()
        {
            ExpeditionExperienceSnapshot snapshot = ExpeditionExperienceSnapshot.Empty;

            AssertSnapshot(snapshot, 0, 0, 0, 0, 0);
        }

        [Test]
        public void NormalRewards_AccumulateIndependentlyAndDeriveTotal()
        {
            ExpeditionExperienceSnapshot snapshot = ExpeditionExperienceSnapshot.Empty;

            snapshot = Apply(snapshot, ExpeditionExperienceCategory.Kill, 10);
            snapshot = Apply(snapshot, ExpeditionExperienceCategory.Assist, 4);
            snapshot = Apply(snapshot, ExpeditionExperienceCategory.Exploration, 6);
            snapshot = Apply(snapshot, ExpeditionExperienceCategory.Kill, 5);

            AssertSnapshot(snapshot, 15, 4, 6, 0, 25);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NormalReward_NonPositiveAmountIsRejectedAtomically(long amount)
        {
            ExpeditionExperienceSnapshot original =
                Apply(ExpeditionExperienceSnapshot.Empty, ExpeditionExperienceCategory.Kill, 7);

            AssertRejected(
                original,
                ExpeditionExperienceCategory.Assist,
                amount,
                ExpeditionExperienceApplicationFailure.InvalidAmount);
        }

        [Test]
        public void NormalReward_UnknownCategoryIsRejectedAtomically()
        {
            ExpeditionExperienceSnapshot original =
                Apply(ExpeditionExperienceSnapshot.Empty, ExpeditionExperienceCategory.Kill, 7);

            AssertRejected(
                original,
                (ExpeditionExperienceCategory)byte.MaxValue,
                1,
                ExpeditionExperienceApplicationFailure.InvalidCategory);
        }

        [Test]
        public void NormalReward_ExtractedLootIsReservedForExtractionResolution()
        {
            ExpeditionExperienceSnapshot original =
                Apply(ExpeditionExperienceSnapshot.Empty, ExpeditionExperienceCategory.Exploration, 3);

            AssertRejected(
                original,
                ExpeditionExperienceCategory.ExtractedLoot,
                10,
                ExpeditionExperienceApplicationFailure.ExtractedLootRequiresExtractionResolution);
        }

        [Test]
        public void NormalReward_CategoryOverflowIsRejectedAtomically()
        {
            ExpeditionExperienceSnapshot original = Apply(
                ExpeditionExperienceSnapshot.Empty,
                ExpeditionExperienceCategory.Kill,
                long.MaxValue);

            AssertRejected(
                original,
                ExpeditionExperienceCategory.Kill,
                1,
                ExpeditionExperienceApplicationFailure.CategoryOverflow);
        }

        [Test]
        public void NormalReward_TotalOverflowIsRejectedAtomically()
        {
            ExpeditionExperienceSnapshot original = Apply(
                ExpeditionExperienceSnapshot.Empty,
                ExpeditionExperienceCategory.Kill,
                long.MaxValue - 1);
            original = Apply(original, ExpeditionExperienceCategory.Assist, 1);

            AssertRejected(
                original,
                ExpeditionExperienceCategory.Exploration,
                1,
                ExpeditionExperienceApplicationFailure.TotalOverflow);
        }

        [Test]
        public void ValidTotalGuaranteesCombatProjectionIsRepresentable()
        {
            ExpeditionExperienceSnapshot snapshot = Apply(
                ExpeditionExperienceSnapshot.Empty,
                ExpeditionExperienceCategory.Kill,
                long.MaxValue - 1);
            snapshot = Apply(snapshot, ExpeditionExperienceCategory.Assist, 1);

            Assert.That(snapshot.TotalExperience, Is.EqualTo(long.MaxValue));
            Assert.That(snapshot.KillExperience + snapshot.AssistExperience,
                Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void ExtractedLootReward_PreservesNormalCategoriesAndAcceptsZero()
        {
            var original = new ExpeditionExperienceSnapshot(3, 4, 5, 0);

            Assert.That(
                ExpeditionExperienceRules.TryApplyExtractedLootReward(
                    original,
                    0,
                    out ExpeditionExperienceSnapshot zeroCandidate,
                    out ExpeditionExperienceApplicationFailure zeroFailure),
                Is.True,
                zeroFailure.ToString());
            Assert.That(zeroCandidate, Is.EqualTo(original));

            Assert.That(
                ExpeditionExperienceRules.TryApplyExtractedLootReward(
                    original,
                    7,
                    out ExpeditionExperienceSnapshot candidate,
                    out ExpeditionExperienceApplicationFailure failure),
                Is.True,
                failure.ToString());
            AssertSnapshot(candidate, 3, 4, 5, 7, 19);
        }

        [Test]
        public void ExtractedLootReward_RejectsNegativeAndOverflowAtomically()
        {
            var original = new ExpeditionExperienceSnapshot(1, 2, 3, 4);
            Assert.That(
                ExpeditionExperienceRules.TryApplyExtractedLootReward(
                    original,
                    -1,
                    out ExpeditionExperienceSnapshot negative,
                    out ExpeditionExperienceApplicationFailure negativeFailure),
                Is.False);
            Assert.That(negativeFailure, Is.EqualTo(ExpeditionExperienceApplicationFailure.InvalidAmount));
            Assert.That(negative, Is.EqualTo(original));

            var categoryFull = new ExpeditionExperienceSnapshot(0, 0, 0, long.MaxValue);
            Assert.That(
                ExpeditionExperienceRules.TryApplyExtractedLootReward(
                    categoryFull,
                    1,
                    out ExpeditionExperienceSnapshot overflow,
                    out ExpeditionExperienceApplicationFailure overflowFailure),
                Is.False);
            Assert.That(overflowFailure, Is.EqualTo(ExpeditionExperienceApplicationFailure.CategoryOverflow));
            Assert.That(overflow, Is.EqualTo(categoryFull));
        }

        private static ExpeditionExperienceSnapshot Apply(
            in ExpeditionExperienceSnapshot current,
            ExpeditionExperienceCategory category,
            long amount)
        {
            Assert.That(
                ExpeditionExperienceRules.TryApplyNormalReward(
                    current,
                    category,
                    amount,
                    out ExpeditionExperienceSnapshot candidate,
                    out ExpeditionExperienceApplicationFailure failure),
                Is.True,
                failure.ToString());
            Assert.That(failure, Is.EqualTo(ExpeditionExperienceApplicationFailure.None));
            return candidate;
        }

        private static void AssertRejected(
            in ExpeditionExperienceSnapshot original,
            ExpeditionExperienceCategory category,
            long amount,
            ExpeditionExperienceApplicationFailure expectedFailure)
        {
            Assert.That(
                ExpeditionExperienceRules.TryApplyNormalReward(
                    original,
                    category,
                    amount,
                    out ExpeditionExperienceSnapshot candidate,
                    out ExpeditionExperienceApplicationFailure failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(expectedFailure));
            Assert.That(candidate, Is.EqualTo(original));
        }

        private static void AssertSnapshot(
            in ExpeditionExperienceSnapshot snapshot,
            long kill,
            long assist,
            long exploration,
            long extractedLoot,
            long total)
        {
            Assert.That(snapshot.KillExperience, Is.EqualTo(kill));
            Assert.That(snapshot.AssistExperience, Is.EqualTo(assist));
            Assert.That(snapshot.ExplorationExperience, Is.EqualTo(exploration));
            Assert.That(snapshot.ExtractedLootExperience, Is.EqualTo(extractedLoot));
            Assert.That(snapshot.TotalExperience, Is.EqualTo(total));
        }
    }
}
