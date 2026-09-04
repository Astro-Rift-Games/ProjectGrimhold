using System.Collections.Generic;

/// <summary>Calculates the fixed Loot-generation chance for an admitted Raid cohort.</summary>
public static class RaidEffectiveLuckCalculator
{
    public static bool TryCalculateAdditionalLootChanceBasisPoints(
        IReadOnlyList<CharacterAttributeState> participantAttributes,
        CharacterDerivedStatisticsConfiguration configuration,
        out int additionalLootChanceBasisPoints)
    {
        additionalLootChanceBasisPoints = 0;
        if (participantAttributes == null || participantAttributes.Count == 0 ||
            configuration == null)
        {
            return false;
        }

        long totalBasisPoints = 0;
        for (int index = 0; index < participantAttributes.Count; index++)
        {
            CharacterAttributeState attributes = participantAttributes[index];
            if (!CharacterDerivedStatisticsCalculator.TryCalculate(
                    attributes,
                    configuration,
                    out CharacterDerivedStatistics statistics,
                    out _))
            {
                return false;
            }

            totalBasisPoints += statistics.AdditionalLootChanceBasisPoints;
        }

        additionalLootChanceBasisPoints =
            (int)(totalBasisPoints / participantAttributes.Count);
        return true;
    }
}
