using System;
using System.Collections.Generic;

/// <summary>
/// Pure projection of resolved Raid loot into the Results screen summary.
/// Extraction reuses the XP eligibility classification so the grid and its total value agree
/// with the Loot value that already feeds experience; Defeat and Abandon report everything carried.
/// </summary>
public static class RaidResultsLootSummaryBuilder
{
    public static bool TryBuildExtracted(
        RaidLootEligibilitySnapshot eligibility,
        IRaidLootValueSource valueSource,
        out RaidResultsLootSummary summary,
        out string error)
    {
        summary = null;
        if (eligibility == null)
        {
            error = "Extracted loot eligibility is required.";
            return false;
        }

        var entries = new List<(LootId LootId, int Amount)>(eligibility.Entries.Count);
        for (int index = 0; index < eligibility.Entries.Count; index++)
        {
            RaidLootEligibilityEntry entry = eligibility.Entries[index];
            if (!entry.IsValid)
            {
                error = "Extracted loot eligibility contains an invalid entry.";
                return false;
            }

            if (entry.EligibleAmount > 0)
            {
                entries.Add((entry.LootId, entry.EligibleAmount));
            }
        }

        return TryBuild(RaidResultsLootKind.Extracted, entries, valueSource, out summary, out error);
    }

    /// <summary>
    /// Lists everything carried. Design only asks to show what was lost, never its Value, so this
    /// path has no Value dependency and a catalog gap can never hide a defeat's lost loot.
    /// </summary>
    public static bool TryBuildLost(
        IReadOnlyList<LootEntry> carriedLoot,
        out RaidResultsLootSummary summary,
        out string error)
    {
        summary = null;
        error = null;
        if (carriedLoot == null)
        {
            error = "Carried loot snapshot is required.";
            return false;
        }

        var seen = new HashSet<LootId>();
        var entries = new RaidResultsLootEntry[carriedLoot.Count];
        for (int index = 0; index < carriedLoot.Count; index++)
        {
            LootEntry entry = carriedLoot[index];
            if (!entry.IsValid || !seen.Add(entry.LootId))
            {
                error = "Carried loot snapshot contains an invalid or duplicate entry.";
                return false;
            }

            entries[index] = new RaidResultsLootEntry(entry.LootId, entry.Amount);
        }

        summary = new RaidResultsLootSummary(RaidResultsLootKind.Lost, entries, 0);
        return true;
    }

    private static bool TryBuild(
        RaidResultsLootKind kind,
        List<(LootId LootId, int Amount)> source,
        IRaidLootValueSource valueSource,
        out RaidResultsLootSummary summary,
        out string error)
    {
        summary = null;
        error = null;
        if (valueSource == null)
        {
            error = "Loot value source is required.";
            return false;
        }

        var seen = new HashSet<LootId>();
        var entries = new RaidResultsLootEntry[source.Count];
        long totalValue = 0;
        try
        {
            for (int index = 0; index < source.Count; index++)
            {
                (LootId lootId, int amount) = source[index];
                if (!seen.Add(lootId))
                {
                    error = "Results loot contains a duplicate LootId.";
                    return false;
                }

                if (!valueSource.TryGetValuePerUnit(lootId, out long valuePerUnit) || valuePerUnit <= 0)
                {
                    error = $"Results loot has no valid Value for '{lootId.Value}'.";
                    return false;
                }

                entries[index] = new RaidResultsLootEntry(lootId, amount);
                totalValue = checked(totalValue + checked(valuePerUnit * amount));
            }
        }
        catch (OverflowException)
        {
            error = "Results loot value overflowed.";
            return false;
        }

        summary = new RaidResultsLootSummary(kind, entries, totalValue);
        return true;
    }
}
