using System.Collections.Generic;

/// <summary>Pure deterministic conversion from eligible Raid loot to provisional experience.</summary>
public static class ExtractedLootExperienceCalculator
{
    public const int BasisPointsDenominator = 10_000;

    public static bool TryCalculate(
        RaidLootEligibilitySnapshot eligibility,
        IRaidLootValueSource valueSource,
        int experienceRateBasisPoints,
        out ExtractedLootExperienceCalculation calculation,
        out ExtractedLootExperienceCalculationFailure failure)
    {
        calculation = default;
        failure = ExtractedLootExperienceCalculationFailure.None;
        if (eligibility == null)
        {
            failure = ExtractedLootExperienceCalculationFailure.MissingEligibilitySnapshot;
            return false;
        }

        if (valueSource == null)
        {
            failure = ExtractedLootExperienceCalculationFailure.MissingValueSource;
            return false;
        }

        if (experienceRateBasisPoints < 1 || experienceRateBasisPoints > BasisPointsDenominator)
        {
            failure = ExtractedLootExperienceCalculationFailure.InvalidPercentage;
            return false;
        }

        IReadOnlyList<RaidLootEligibilityEntry> entries = eligibility.Entries;
        var seenLootIds = new HashSet<LootId>();
        long resolvedTotalAmount = 0;
        long resolvedEligibleAmount = 0;
        long eligibleValue = 0;
        for (int index = 0; index < entries.Count; index++)
        {
            RaidLootEligibilityEntry entry = entries[index];
            if (!entry.IsValid)
            {
                failure = ExtractedLootExperienceCalculationFailure.InvalidEligibilityEntry;
                return false;
            }

            if (!seenLootIds.Add(entry.LootId))
            {
                failure = ExtractedLootExperienceCalculationFailure.DuplicateLootId;
                return false;
            }

            if (resolvedTotalAmount > long.MaxValue - entry.TotalAmount ||
                resolvedEligibleAmount > long.MaxValue - entry.EligibleAmount)
            {
                failure = ExtractedLootExperienceCalculationFailure.InconsistentEligibilityTotals;
                return false;
            }

            resolvedTotalAmount += entry.TotalAmount;
            resolvedEligibleAmount += entry.EligibleAmount;
            if (entry.EligibleAmount == 0)
            {
                continue;
            }

            if (!valueSource.TryGetValuePerUnit(entry.LootId, out long valuePerUnit) ||
                valuePerUnit <= 0)
            {
                failure = ExtractedLootExperienceCalculationFailure.MissingOrInvalidValue;
                return false;
            }

            if (valuePerUnit > long.MaxValue / entry.EligibleAmount)
            {
                failure = ExtractedLootExperienceCalculationFailure.ValueMultiplicationOverflow;
                return false;
            }

            long entryValue = valuePerUnit * entry.EligibleAmount;
            if (eligibleValue > long.MaxValue - entryValue)
            {
                failure = ExtractedLootExperienceCalculationFailure.EligibleValueOverflow;
                return false;
            }

            eligibleValue += entryValue;
        }

        if (resolvedTotalAmount != eligibility.TotalAmount ||
            resolvedEligibleAmount != eligibility.EligibleAmount ||
            eligibility.TotalAmount < 0 || eligibility.EligibleAmount < 0 ||
            eligibility.EligibleAmount > eligibility.TotalAmount)
        {
            failure = ExtractedLootExperienceCalculationFailure.InconsistentEligibilityTotals;
            return false;
        }

        long quotient = eligibleValue / BasisPointsDenominator;
        long remainder = eligibleValue % BasisPointsDenominator;
        if (quotient > long.MaxValue / experienceRateBasisPoints)
        {
            failure = ExtractedLootExperienceCalculationFailure.PercentageOverflow;
            return false;
        }

        long awardedExperience = quotient * experienceRateBasisPoints;
        long remainderExperience = remainder * experienceRateBasisPoints / BasisPointsDenominator;
        if (awardedExperience > long.MaxValue - remainderExperience)
        {
            failure = ExtractedLootExperienceCalculationFailure.PercentageOverflow;
            return false;
        }

        calculation = new ExtractedLootExperienceCalculation(
            eligibleValue,
            awardedExperience + remainderExperience);
        return true;
    }
}
