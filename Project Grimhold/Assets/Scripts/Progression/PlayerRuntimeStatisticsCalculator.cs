/// <summary>Combines character-derived statistics with the current equipment projection.</summary>
public static class PlayerRuntimeStatisticsCalculator
{
    public static bool TryCalculate(
        in CharacterAttributeState attributes,
        CharacterDerivedStatisticsConfiguration configuration,
        in EquipmentStatisticsModifiers equipment,
        out PlayerRuntimeStatistics statistics,
        out CharacterDerivedStatisticsCalculationFailure failure)
    {
        statistics = default;
        if (!CharacterDerivedStatisticsCalculator.TryCalculate(
                attributes,
                configuration,
                equipment,
                out CharacterDerivedStatistics characterStatistics,
                out failure))
        {
            return false;
        }

        statistics = new PlayerRuntimeStatistics(
            characterStatistics,
            equipment.PhysicalDefense,
            equipment.MagicalDefense);
        return true;
    }
}
