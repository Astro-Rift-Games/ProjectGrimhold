/// <summary>Immutable balance configuration for character-derived statistics.</summary>
public sealed class CharacterDerivedStatisticsConfiguration
{
    public int BaseMaximumHealth { get; }
    public int MaximumHealthPerVitality { get; }
    public int BaseMaximumStamina { get; }
    public int MaximumStaminaPerResistance { get; }
    public int AdditionalLootChanceBasisPointsPerLuck { get; }
    public int MaximumAdditionalLootChanceBasisPoints { get; }

    private CharacterDerivedStatisticsConfiguration(
        int baseMaximumHealth,
        int maximumHealthPerVitality,
        int baseMaximumStamina,
        int maximumStaminaPerResistance,
        int additionalLootChanceBasisPointsPerLuck,
        int maximumAdditionalLootChanceBasisPoints)
    {
        BaseMaximumHealth = baseMaximumHealth;
        MaximumHealthPerVitality = maximumHealthPerVitality;
        BaseMaximumStamina = baseMaximumStamina;
        MaximumStaminaPerResistance = maximumStaminaPerResistance;
        AdditionalLootChanceBasisPointsPerLuck = additionalLootChanceBasisPointsPerLuck;
        MaximumAdditionalLootChanceBasisPoints = maximumAdditionalLootChanceBasisPoints;
    }

    public static bool TryCreate(
        int baseMaximumHealth,
        int maximumHealthPerVitality,
        int baseMaximumStamina,
        int maximumStaminaPerResistance,
        int additionalLootChanceBasisPointsPerLuck,
        int maximumAdditionalLootChanceBasisPoints,
        out CharacterDerivedStatisticsConfiguration configuration)
    {
        configuration = null;
        if (baseMaximumHealth < 0 ||
            maximumHealthPerVitality < 0 ||
            baseMaximumStamina < 0 ||
            maximumStaminaPerResistance < 0 ||
            additionalLootChanceBasisPointsPerLuck < 0 ||
            maximumAdditionalLootChanceBasisPoints < 0 ||
            maximumAdditionalLootChanceBasisPoints > CharacterDerivedStatisticsCalculator.BasisPointsDenominator)
        {
            return false;
        }

        configuration = new CharacterDerivedStatisticsConfiguration(
            baseMaximumHealth,
            maximumHealthPerVitality,
            baseMaximumStamina,
            maximumStaminaPerResistance,
            additionalLootChanceBasisPointsPerLuck,
            maximumAdditionalLootChanceBasisPoints);
        return true;
    }
}
