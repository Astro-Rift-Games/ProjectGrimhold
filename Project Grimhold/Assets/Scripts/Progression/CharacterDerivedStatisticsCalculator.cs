using System;

/// <summary>Pure deterministic conversion from character attributes to their derived statistics.</summary>
public static class CharacterDerivedStatisticsCalculator
{
    public const int BasisPointsDenominator = 10_000;

    public static bool TryCalculate(
        in CharacterAttributeState attributes,
        CharacterDerivedStatisticsConfiguration configuration,
        out CharacterDerivedStatistics statistics,
        out CharacterDerivedStatisticsCalculationFailure failure) =>
        TryCalculate(attributes, configuration, default, out statistics, out failure);

    public static bool TryCalculate(
        in CharacterAttributeState attributes,
        CharacterDerivedStatisticsConfiguration configuration,
        in EquipmentStatisticsModifiers externalModifiers,
        out CharacterDerivedStatistics statistics,
        out CharacterDerivedStatisticsCalculationFailure failure)
    {
        statistics = default;
        failure = CharacterDerivedStatisticsCalculationFailure.None;
        if (configuration == null)
        {
            failure = CharacterDerivedStatisticsCalculationFailure.MissingConfiguration;
            return false;
        }

        if (!TryCalculateMaximum(
                configuration.BaseMaximumHealth,
                configuration.MaximumHealthPerVitality,
                attributes.Vitality,
                externalModifiers.MaximumHealthModifier,
                out int maximumHealth))
        {
            failure = CharacterDerivedStatisticsCalculationFailure.MaximumHealthOverflow;
            return false;
        }

        if (!TryCalculateMaximum(
                configuration.BaseMaximumStamina,
                configuration.MaximumStaminaPerResistance,
                attributes.Resistance,
                externalModifiers.MaximumStaminaModifier,
                out int maximumStamina))
        {
            failure = CharacterDerivedStatisticsCalculationFailure.MaximumStaminaOverflow;
            return false;
        }

        if (!TryCalculateMaximum(
                configuration.BaseMaximumMana,
                0,
                0,
                externalModifiers.MaximumManaModifier,
                out int maximumMana))
        {
            failure = CharacterDerivedStatisticsCalculationFailure.MaximumManaOverflow;
            return false;
        }

        long uncappedAdditionalLootChance =
            (long)attributes.Luck * configuration.AdditionalLootChanceBasisPointsPerLuck;
        int additionalLootChanceBasisPoints = (int)Math.Min(
            uncappedAdditionalLootChance,
            configuration.MaximumAdditionalLootChanceBasisPoints);

        statistics = new CharacterDerivedStatistics(
            maximumHealth,
            maximumStamina,
            maximumMana,
            additionalLootChanceBasisPoints);
        return true;
    }

    private static bool TryCalculateMaximum(
        int baseValue,
        int valuePerAttribute,
        int attribute,
        int externalModifier,
        out int result)
    {
        long candidate = baseValue + (long)valuePerAttribute * attribute + externalModifier;
        if (candidate > int.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (int)candidate;
        return true;
    }
}
