using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Local provisional source of progression Value. It is intentionally independent from
/// extraction-progress and economic values on <see cref="LootDefinition"/>.
/// </summary>
[CreateAssetMenu(fileName = "RaidLootValueCatalog", menuName = "Grimhold/Progression/Raid Loot Value Catalog")]
public sealed class RaidLootValueCatalog : ScriptableObject, IRaidLootValueSource
{
    [SerializeField]
    private List<RaidLootValueEntry> _entries = new();

    public bool TryGetValuePerUnit(LootId lootId, out long valuePerUnit)
    {
        valuePerUnit = 0;
        if (!lootId.IsValid || _entries == null)
        {
            return false;
        }

        bool found = false;
        for (int index = 0; index < _entries.Count; index++)
        {
            RaidLootValueEntry entry = _entries[index];
            if (entry?.LootDefinition == null ||
                string.IsNullOrWhiteSpace(entry.LootDefinition.Id) ||
                entry.ValuePerUnit <= 0)
            {
                continue;
            }

            if (!string.Equals(entry.LootDefinition.Id, lootId.Value, StringComparison.Ordinal))
            {
                continue;
            }

            if (found)
            {
                valuePerUnit = 0;
                return false;
            }

            valuePerUnit = entry.ValuePerUnit;
            found = true;
        }

        return found;
    }

    public bool TryValidate(LootDefinitionCatalog lootCatalog, out string error)
    {
        error = null;
        if (lootCatalog == null)
        {
            error = "Productive loot catalog is missing.";
            return false;
        }

        if (!lootCatalog.TryValidate(out string catalogError))
        {
            error = $"Productive loot catalog is invalid: {catalogError}.";
            return false;
        }

        if (_entries == null || _entries.Count == 0)
        {
            error = "Raid loot Value catalog has no entries.";
            return false;
        }

        var seenLootIds = new HashSet<LootId>();
        for (int index = 0; index < _entries.Count; index++)
        {
            RaidLootValueEntry entry = _entries[index];
            if (entry?.LootDefinition == null)
            {
                error = "Raid loot Value catalog contains a missing loot definition.";
                return false;
            }

            if (!entry.LootDefinition.TryValidate(out string definitionError))
            {
                error = $"Raid loot Value catalog contains an invalid loot definition: {definitionError}.";
                return false;
            }

            if (entry.ValuePerUnit <= 0)
            {
                error = $"Raid loot Value for '{entry.LootDefinition.Id}' must be positive.";
                return false;
            }

            if (!seenLootIds.Add(entry.LootDefinition.LootId))
            {
                error = $"Raid loot Value catalog contains duplicate LootId '{entry.LootDefinition.Id}'.";
                return false;
            }

            if (!lootCatalog.TryGetIndex(entry.LootDefinition.LootId, out _))
            {
                error = $"Raid loot Value references non-productive LootId '{entry.LootDefinition.Id}'.";
                return false;
            }
        }

        if (seenLootIds.Count != lootCatalog.DefinitionCount)
        {
            error = "Raid loot Value catalog does not cover every productive LootId.";
            return false;
        }

        for (int index = 0; index < lootCatalog.DefinitionCount; index++)
        {
            if (!lootCatalog.TryGetByIndex(index, out LootDefinition definition) ||
                !seenLootIds.Contains(definition.LootId))
            {
                error = $"Raid loot Value is missing for productive catalog index {index}.";
                return false;
            }
        }

        return true;
    }
}
