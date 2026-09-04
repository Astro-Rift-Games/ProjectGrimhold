using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class RaidEffectiveLuckCalculatorTests
    {
        [Test]
        public void Calculate_SoloUsesTheAdmittedParticipantLuck()
        {
            CharacterAttributeState attributes = CreateAttributes(17);

            Assert.That(TryCalculate(new[] { attributes }, out int chance), Is.True);
            Assert.That(chance, Is.EqualTo(1_700));
        }

        [Test]
        public void Calculate_ZeroLuckProducesZeroChance()
        {
            Assert.That(TryCalculate(new[] { CreateAttributes(0) }, out int chance), Is.True);
            Assert.That(chance, Is.Zero);
        }

        [Test]
        public void Calculate_GroupAveragesBasisPointsWithoutLosingHalfPercentPrecision()
        {
            CharacterAttributeState first = CreateAttributes(5);
            CharacterAttributeState second = CreateAttributes(6);

            Assert.That(TryCalculate(new[] { first, second }, out int chance), Is.True);
            Assert.That(chance, Is.EqualTo(550));
        }

        [Test]
        public void Calculate_UsesTheCurrentThirtyPercentCap()
        {
            CharacterAttributeState first = CreateAttributes(30);
            CharacterAttributeState second = CreateAttributes(40);

            Assert.That(TryCalculate(new[] { first, second }, out int chance), Is.True);
            Assert.That(chance, Is.EqualTo(3_000));
        }

        [Test]
        public void Calculate_MissingCohortOrConfigurationIsRejected()
        {
            Assert.That(TryCalculate(System.Array.Empty<CharacterAttributeState>(), out _), Is.False);
            Assert.That(
                RaidEffectiveLuckCalculator.TryCalculateAdditionalLootChanceBasisPoints(
                    new[] { CreateAttributes(5) },
                    null,
                    out _),
                Is.False);
        }

        private static bool TryCalculate(
            CharacterAttributeState[] attributes,
            out int chance) =>
            RaidEffectiveLuckCalculator.TryCalculateAdditionalLootChanceBasisPoints(
                attributes,
                ProgressionBalanceDefaults.InitialCharacterDerivedStatisticsConfiguration,
                out chance);

        private static CharacterAttributeState CreateAttributes(int luck)
        {
            Assert.That(CharacterAttributeState.TryCreate(
                0, 0, 0, 0, 0, luck, 0, out CharacterAttributeState attributes), Is.True);
            return attributes;
        }
    }
}
