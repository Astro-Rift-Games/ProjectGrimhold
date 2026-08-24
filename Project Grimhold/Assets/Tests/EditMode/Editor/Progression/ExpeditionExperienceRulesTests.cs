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
