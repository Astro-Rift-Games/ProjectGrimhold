using System;
using System.Collections.Generic;

/// <summary>
/// Materializes and validates loot-table assets into immutable runtime snapshots.
/// </summary>
public static class LootContainerContentTableValidation
{
    /// <summary>
    /// Validates the complete table/catalog/capacity combination and copies it into an immutable snapshot.
    /// Failure always returns a null snapshot.
    /// </summary>
    public static bool TryCreateSnapshot(
        LootContainerContentTable table,
        LootDefinitionCatalog catalog,
        int slotCapacity,
        int distinctLootCapacity,
        out ValidatedLootContainerContentSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = null;

        if (table == null)
        {
            error = "Loot content table is missing.";
            return false;
        }

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

        int minimum = table.MinimumDistinctStacks;
        int configuredMaximum = table.MaximumDistinctStacks;
        if (minimum < 0 || configuredMaximum < minimum)
        {
            error = "Distinct stack range is invalid.";
            return false;
        }

        if (!table.AllowEmpty && minimum < 1)
        {
            error = "A table that disallows empty results must require at least one stack.";
            return false;
        }

        IReadOnlyList<LootContainerContentTableEntry> configuredEntries = table.Entries;
        int entryCount = configuredEntries?.Count ?? 0;
        int effectiveMaximum = Math.Min(
            configuredMaximum,
            Math.Min(entryCount, Math.Min(slotCapacity, distinctLootCapacity)));
        if (minimum > effectiveMaximum)
        {
            error = "The configured minimum stack count cannot be satisfied by the eligible entries and capacities.";
            return false;
        }

        if (entryCount == 0)
        {
            if (!table.AllowEmpty || minimum != 0 || effectiveMaximum != 0)
            {
                error = "Loot content table has no eligible entries.";
                return false;
            }

            snapshot = new ValidatedLootContainerContentSnapshot(
                Array.Empty<ValidatedLootContainerContentSnapshot.Entry>(),
                0,
                0,
                true,
                0,
                slotCapacity,
                distinctLootCapacity);
            return true;
        }

        var entries = new ValidatedLootContainerContentSnapshot.Entry[entryCount];
        var seenLootIds = new HashSet<LootId>();
        ulong totalWeight = 0;

        for (int i = 0; i < entryCount; i++)
        {
            LootContainerContentTableEntry configuredEntry = configuredEntries[i];
            LootDefinition definition = configuredEntry.Definition;
            string definitionError = null;
            if (definition == null || !definition.TryValidate(out definitionError))
            {
                error = $"Loot table entry {i} has no valid definition. {definitionError}";
                return false;
            }

            if (!catalog.TryGetIndex(definition.LootId, out int catalogIndex) ||
                catalogIndex < 0 || catalogIndex >= RaidLootOriginPackedBuffer.MaximumCatalogEntries)
            {
                error = $"Loot table entry {i} is not representable by the assigned catalog.";
                return false;
            }

            if (!seenLootIds.Add(definition.LootId))
            {
                error = $"Loot table contains duplicate loot ID '{definition.LootId}'.";
                return false;
            }

            if (configuredEntry.Weight == 0)
            {
                error = $"Loot table entry {i} has zero weight.";
                return false;
            }

            if (configuredEntry.MinimumAmount <= 0 ||
                configuredEntry.MaximumAmount <= 0 ||
                configuredEntry.MinimumAmount > configuredEntry.MaximumAmount)
            {
                error = $"Loot table entry {i} has an invalid amount range.";
                return false;
            }

            try
            {
                totalWeight = checked(totalWeight + configuredEntry.Weight);
            }
            catch (OverflowException)
            {
                error = "Loot table total weight exceeds UInt64 capacity.";
                return false;
            }

            entries[i] = new ValidatedLootContainerContentSnapshot.Entry(
                definition.LootId,
                catalogIndex,
                configuredEntry.Weight,
                configuredEntry.MinimumAmount,
                configuredEntry.MaximumAmount);
        }

        snapshot = new ValidatedLootContainerContentSnapshot(
            entries,
            minimum,
            effectiveMaximum,
            table.AllowEmpty,
            totalWeight,
            slotCapacity,
            distinctLootCapacity);
        return true;
    }

    /// <summary>
    /// Verifies that a Luck-compatible source can always add one distinct stack
    /// beyond its configured base maximum.
    /// </summary>
    public static bool HasAdditionalStackCapacity(
        ValidatedLootContainerContentSnapshot snapshot,
        out string error)
    {
        error = null;
        if (snapshot == null)
        {
            error = "Validated loot snapshot is missing.";
            return false;
        }

        int maximumRepresentableStacks = Math.Min(
            snapshot.EntryCount,
            Math.Min(snapshot.SlotCapacity, snapshot.NetworkCapacity));
        if (snapshot.MaximumDistinctStacks >= maximumRepresentableStacks)
        {
            error = "Luck-compatible Loot requires capacity for one distinct stack beyond the configured base maximum.";
            return false;
        }

        return true;
    }
}
