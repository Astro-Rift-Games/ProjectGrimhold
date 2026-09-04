using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class CharacterDerivedStatisticsCalculatorTests
    {
        [TestCase(0, 0, 0, 75, 75, 0)]
        [TestCase(5, 5, 5, 100, 100, 500)]
        [TestCase(25, 25, 25, 200, 200, 2_500)]
        [TestCase(30, 30, 30, 225, 225, 3_000)]
        public void Calculate_UsesDocumentedInitialFormulas(
            int vitality,
            int resistance,
            int luck,
            int expectedMaximumHealth,
            int expectedMaximumStamina,
            int expectedAdditionalLootChanceBasisPoints)
        {
            CharacterAttributeState attributes = CreateAttributes(vitality, resistance, 0, 0, 0, luck, 0);

            Assert.That(TryCalculate(
                attributes,
                ProgressionBalanceDefaults.InitialCharacterDerivedStatisticsConfiguration,
                out CharacterDerivedStatistics statistics,
                out CharacterDerivedStatisticsCalculationFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(CharacterDerivedStatisticsCalculationFailure.None));
            Assert.That(statistics.MaximumHealth, Is.EqualTo(expectedMaximumHealth));
            Assert.That(statistics.MaximumStamina, Is.EqualTo(expectedMaximumStamina));
            Assert.That(statistics.AdditionalLootChanceBasisPoints,
                Is.EqualTo(expectedAdditionalLootChanceBasisPoints));
        }

        [Test]
        public void Calculate_LuckBelowBalanceMaximumIsNotCapped()
        {
            CharacterAttributeState attributes = CreateAttributes(0, 0, 0, 0, 0, 26, 0);

            Assert.That(TryCalculate(
                attributes,
                ProgressionBalanceDefaults.InitialCharacterDerivedStatisticsConfiguration,
                out CharacterDerivedStatistics statistics,
                out _), Is.True);
            Assert.That(statistics.AdditionalLootChanceBasisPoints, Is.EqualTo(2_600));
        }

        [Test]
        public void Calculate_UsesAlternativeValidConfiguration()
        {
            Assert.That(CharacterDerivedStatisticsConfiguration.TryCreate(
                10, 2, 20, 3, 25, 1_000, out CharacterDerivedStatisticsConfiguration configuration), Is.True);
            CharacterAttributeState attributes = CreateAttributes(4, 5, 0, 0, 0, 6, 0);

            Assert.That(TryCalculate(attributes, configuration, out CharacterDerivedStatistics statistics, out _),
                Is.True);
            Assert.That(statistics.MaximumHealth, Is.EqualTo(18));
            Assert.That(statistics.MaximumStamina, Is.EqualTo(35));
            Assert.That(statistics.AdditionalLootChanceBasisPoints, Is.EqualTo(150));
        }

        [Test]
        public void Calculate_IgnoresCompetenciesAndAvailablePoints()
        {
            CharacterAttributeState first = CreateAttributes(5, 6, 0, 0, 0, 7, 0);
            CharacterAttributeState second = CreateAttributes(5, 6, 20, 21, 22, 7, 99);

            Assert.That(TryCalculate(first, InitialConfiguration, out CharacterDerivedStatistics expected, out _),
                Is.True);
            Assert.That(TryCalculate(second, InitialConfiguration, out CharacterDerivedStatistics actual, out _),
                Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Calculate_RepeatedInputIsDeterministic()
        {
            CharacterAttributeState attributes = CreateAttributes(8, 9, 10, 11, 12, 13, 14);

            Assert.That(TryCalculate(attributes, InitialConfiguration, out CharacterDerivedStatistics first, out _),
                Is.True);
            Assert.That(TryCalculate(attributes, InitialConfiguration, out CharacterDerivedStatistics second, out _),
                Is.True);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(second.GetHashCode(), Is.EqualTo(first.GetHashCode()));
        }

        [Test]
        public void Calculate_MissingConfigurationIsRejectedWithoutPartialResult()
        {
            CharacterAttributeState attributes = CreateAttributes(5, 5, 5, 5, 5, 5, 10);

            AssertFailure(
                attributes,
                null,
                CharacterDerivedStatisticsCalculationFailure.MissingConfiguration);
        }

        [Test]
        public void Calculate_MaximumHealthOverflowIsRejectedWithoutPartialResult()
        {
            CharacterDerivedStatisticsConfiguration configuration = CreateConfiguration(
                0, int.MaxValue, 0, 0, 0, 0);
            CharacterAttributeState attributes = CreateAttributes(int.MaxValue, 0, 0, 0, 0, 0, 0);

            AssertFailure(
                attributes,
                configuration,
                CharacterDerivedStatisticsCalculationFailure.MaximumHealthOverflow);
        }

        [Test]
        public void Calculate_MaximumStaminaOverflowIsRejectedWithoutPartialResult()
        {
            CharacterDerivedStatisticsConfiguration configuration = CreateConfiguration(
                0, 0, 0, int.MaxValue, 0, 0);
            CharacterAttributeState attributes = CreateAttributes(0, int.MaxValue, 0, 0, 0, 0, 0);

            AssertFailure(
                attributes,
                configuration,
                CharacterDerivedStatisticsCalculationFailure.MaximumStaminaOverflow);
        }

        [TestCase(-1, 0, 0, 0, 0, 0)]
        [TestCase(0, -1, 0, 0, 0, 0)]
        [TestCase(0, 0, -1, 0, 0, 0)]
        [TestCase(0, 0, 0, -1, 0, 0)]
        [TestCase(0, 0, 0, 0, -1, 0)]
        [TestCase(0, 0, 0, 0, 0, -1)]
        [TestCase(0, 0, 0, 0, 0, 10_001)]
        public void Configuration_InvalidValueIsRejected(
            int baseMaximumHealth,
            int maximumHealthPerVitality,
            int baseMaximumStamina,
            int maximumStaminaPerResistance,
            int additionalLootChanceBasisPointsPerLuck,
            int maximumAdditionalLootChanceBasisPoints)
        {
            Assert.That(CharacterDerivedStatisticsConfiguration.TryCreate(
                baseMaximumHealth,
                maximumHealthPerVitality,
                baseMaximumStamina,
                maximumStaminaPerResistance,
                additionalLootChanceBasisPointsPerLuck,
                maximumAdditionalLootChanceBasisPoints,
                out CharacterDerivedStatisticsConfiguration configuration), Is.False);
            Assert.That(configuration, Is.Null);
        }

        private static CharacterDerivedStatisticsConfiguration InitialConfiguration =>
            ProgressionBalanceDefaults.InitialCharacterDerivedStatisticsConfiguration;

        private static bool TryCalculate(
            in CharacterAttributeState attributes,
            CharacterDerivedStatisticsConfiguration configuration,
            out CharacterDerivedStatistics statistics,
            out CharacterDerivedStatisticsCalculationFailure failure) =>
            CharacterDerivedStatisticsCalculator.TryCalculate(
                attributes,
                configuration,
                out statistics,
                out failure);

        private static CharacterAttributeState CreateAttributes(
            int vitality,
            int resistance,
            int strength,
            int dexterity,
            int intelligence,
            int luck,
            int availablePoints)
        {
            Assert.That(CharacterAttributeState.TryCreate(
                vitality,
                resistance,
                strength,
                dexterity,
                intelligence,
                luck,
                availablePoints,
                out CharacterAttributeState attributes), Is.True);
            return attributes;
        }

        private static CharacterDerivedStatisticsConfiguration CreateConfiguration(
            int baseMaximumHealth,
            int maximumHealthPerVitality,
            int baseMaximumStamina,
            int maximumStaminaPerResistance,
            int additionalLootChanceBasisPointsPerLuck,
            int maximumAdditionalLootChanceBasisPoints)
        {
            Assert.That(CharacterDerivedStatisticsConfiguration.TryCreate(
                baseMaximumHealth,
                maximumHealthPerVitality,
                baseMaximumStamina,
                maximumStaminaPerResistance,
                additionalLootChanceBasisPointsPerLuck,
                maximumAdditionalLootChanceBasisPoints,
                out CharacterDerivedStatisticsConfiguration configuration), Is.True);
            return configuration;
        }

        private static void AssertFailure(
            in CharacterAttributeState attributes,
            CharacterDerivedStatisticsConfiguration configuration,
            CharacterDerivedStatisticsCalculationFailure expectedFailure)
        {
            Assert.That(TryCalculate(
                attributes,
                configuration,
                out CharacterDerivedStatistics statistics,
                out CharacterDerivedStatisticsCalculationFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(expectedFailure));
            Assert.That(statistics, Is.EqualTo(default(CharacterDerivedStatistics)));
        }
    }
}
