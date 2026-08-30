using System;
using System.Collections.Generic;

/// <summary>
/// Builds a fully validated, deterministic initial representation for a loot container.
/// </summary>
public static class LootContainerInitializationRules
{
    /// <summary>
    /// Resolves initial entries to catalog indices without returning partial output on failure.
    /// </summary>
    public static bool TryBuild(
        IReadOnlyList<LootContainerInitialEntry> initialEntries,
        LootDefinitionCatalog catalog,
        int slotCapacity,
        int distinctLootCapacity,
        out IReadOnlyList<KeyValuePair<int, int>> resolvedEntries,
        out string error)
    {
        resolvedEntries = Array.Empty<KeyValuePair<int, int>>();
        error = null;

        int count = initialEntries?.Count ?? 0;
        var normalized = new List<LootEntry>(count);
        for (int i = 0; i < count; i++)
        {
            LootContainerInitialEntry entry = initialEntries[i];
            string definitionError = null;
            if (entry.Definition == null || !entry.Definition.TryValidate(out definitionError))
            {
                error = $"Initial entry {i} has no valid definition. {definitionError}";
                return false;
            }

            normalized.Add(new LootEntry(entry.Definition.LootId, entry.Amount));
        }

        return TryBuild(
            normalized,
            catalog,
            slotCapacity,
            distinctLootCapacity,
            out resolvedEntries,
            out error);
    }

    /// <summary>
    /// Resolves already materialized runtime stacks to catalog indices without partial output on failure.
    /// </summary>
    public static bool TryBuild(
        IReadOnlyList<LootEntry> initialEntries,
        LootDefinitionCatalog catalog,
        int slotCapacity,
        int distinctLootCapacity,
        out IReadOnlyList<KeyValuePair<int, int>> resolvedEntries,
        out string error)
    {
        resolvedEntries = Array.Empty<KeyValuePair<int, int>>();
        error = null;

        string catalogError = null;
        if (catalog == null || !catalog.TryValidate(out catalogError))
        {
            error = $"Loot catalog is missing or invalid. {catalogError}";
            return false;
        }

        if (!LootInventoryRules.IsValidSlotCapacity(slotCapacity, distinctLootCapacity))
        {
            error = $"Slot capacity must be between 1 and {distinctLootCapacity}.";
            return false;
        }

        if (catalog.DefinitionCount > RaidLootOriginPackedBuffer.MaximumCatalogEntries)
        {
            error = $"Catalog contains {catalog.DefinitionCount} definitions but only {RaidLootOriginPackedBuffer.MaximumCatalogEntries} indices can be represented.";
            return false;
        }

        int count = initialEntries?.Count ?? 0;
        if (count > slotCapacity || count > distinctLootCapacity)
        {
            error = "Initial content exceeds the configured slot capacity.";
            return false;
        }

        var entries = new List<KeyValuePair<int, int>>(count);
        var seenIndices = new HashSet<int>();
        for (int i = 0; i < count; i++)
        {
            LootEntry entry = initialEntries[i];
            if (!entry.IsValid)
            {
                error = $"Initial entry {i} has an invalid loot ID or non-positive amount.";
                return false;
            }

            if (!catalog.TryGetIndex(entry.LootId, out int catalogIndex) ||
                catalogIndex < 0 || catalogIndex >= RaidLootOriginPackedBuffer.MaximumCatalogEntries)
            {
                error = $"Initial entry {i} is not representable by the assigned catalog.";
                return false;
            }

            if (!seenIndices.Add(catalogIndex))
            {
                error = $"Initial content contains duplicate catalog index {catalogIndex}.";
                return false;
            }

            entries.Add(new KeyValuePair<int, int>(catalogIndex, entry.Amount));
        }

        entries.Sort((left, right) => left.Key.CompareTo(right.Key));
        resolvedEntries = entries.AsReadOnly();
        return true;
    }
}
