using System;
using System.Collections.Generic;

/// <summary>
/// Pure, deterministic conversion between a <see cref="RaidResultsLootSummary"/> and the
/// catalog-index / amount arrays replicated on the participant.
/// </summary>
public static class RaidResultsLootWire
{
    public delegate bool TryGetCatalogIndex(LootId lootId, out int index);
    public delegate bool TryGetLootId(int index, out LootId lootId);

    public static bool TryEncode(
        RaidResultsLootSummary summary,
        TryGetCatalogIndex tryGetIndex,
        int maximumEntries,
        int maximumCatalogEntries,
        out int[] indices,
        out int[] amounts,
        out string error)
    {
        indices = null;
        amounts = null;
        error = null;
        if (summary == null || summary.Kind == RaidResultsLootKind.None || tryGetIndex == null ||
            summary.TotalValue < 0)
        {
            error = "Results loot summary is missing, empty-kind or has a negative value.";
            return false;
        }

        if (summary.Entries.Count > maximumEntries)
        {
            error = "Results loot summary exceeds the replicated capacity.";
            return false;
        }

        var ordered = new List<(int Index, int Amount)>(summary.Entries.Count);
        var seen = new HashSet<int>();
        for (int i = 0; i < summary.Entries.Count; i++)
        {
            RaidResultsLootEntry entry = summary.Entries[i];
            if (!entry.IsValid ||
                !tryGetIndex(entry.LootId, out int index) ||
                index < 0 || index >= maximumCatalogEntries ||
                !seen.Add(index))
            {
                error = "Results loot summary contains an invalid, unknown or duplicate entry.";
                return false;
            }

            ordered.Add((index, entry.Amount));
        }

        ordered.Sort((left, right) => left.Index.CompareTo(right.Index));
        indices = new int[ordered.Count];
        amounts = new int[ordered.Count];
        for (int i = 0; i < ordered.Count; i++)
        {
            indices[i] = ordered[i].Index;
            amounts[i] = ordered[i].Amount;
        }

        return true;
    }

    public static bool TryDecode(
        RaidResultsLootKind kind,
        long totalValue,
        IReadOnlyList<int> indices,
        IReadOnlyList<int> amounts,
        TryGetLootId tryGetLootId,
        out RaidResultsLootSummary summary)
    {
        summary = null;
        if (kind == RaidResultsLootKind.None || totalValue < 0 || indices == null ||
            amounts == null || indices.Count != amounts.Count || tryGetLootId == null)
        {
            return false;
        }

        var entries = new RaidResultsLootEntry[indices.Count];
        for (int i = 0; i < indices.Count; i++)
        {
            if (amounts[i] <= 0 || !tryGetLootId(indices[i], out LootId lootId) || !lootId.IsValid)
            {
                return false;
            }

            entries[i] = new RaidResultsLootEntry(lootId, amounts[i]);
        }

        summary = new RaidResultsLootSummary(kind, entries, totalValue);
        return true;
    }
}
