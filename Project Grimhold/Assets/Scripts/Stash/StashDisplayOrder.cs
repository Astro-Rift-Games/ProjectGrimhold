using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Disposable Stash-screen ordering; never reorders persistent inventory or Equipment.</summary>
public static class StashDisplayOrder
{
    public static List<StashItem> SortedCopy(IReadOnlyList<StashItem> source, LootDefinitionCatalog catalog)
    {
        var copy = source == null ? new List<StashItem>() : new List<StashItem>(source);
        copy.Sort((left, right) => Compare(left, right, catalog));
        return copy;
    }

    public static IReadOnlyList<RaidInventorySlotData> Project(
        IReadOnlyList<StashItem> source, LootDefinitionCatalog catalog, Sprite placeholder)
    {
        var result = new List<RaidInventorySlotData>();
        foreach (StashItem item in SortedCopy(source, catalog))
        {
            LootDefinition definition = Resolve(item, catalog);
            var slot = RaidInventorySlotData.Create(new LootEntry(item.LootId, item.Amount), definition, placeholder);
            if (slot.IsOccupied && string.IsNullOrWhiteSpace(slot.DisplayName))
                slot = slot.WithDisplayName(item.LootId.Value);
            result.Add(slot);
        }
        return result;
    }

    private static int Compare(StashItem left, StashItem right, LootDefinitionCatalog catalog)
    {
        LootDefinition leftDefinition = Resolve(left, catalog);
        LootDefinition rightDefinition = Resolve(right, catalog);
        int order = Rank(leftDefinition).CompareTo(Rank(rightDefinition));
        if (order != 0) return order;
        order = StringComparer.OrdinalIgnoreCase.Compare(Name(left, leftDefinition), Name(right, rightDefinition));
        return order != 0 ? order : StringComparer.Ordinal.Compare(left.LootId.Value, right.LootId.Value);
    }

    private static LootDefinition Resolve(StashItem item, LootDefinitionCatalog catalog)
    {
        LootDefinition definition = null;
        if (catalog != null) catalog.TryGet(item.LootId.Value, out definition);
        return definition;
    }

    private static int Rank(LootDefinition definition)
    {
        int category = definition == null ? 0 : (int)definition.Category;
        return category >= (int)LootCategory.Valuable && category <= (int)LootCategory.Shield
            ? category : int.MaxValue;
    }

    private static string Name(StashItem item, LootDefinition definition) =>
        definition == null || string.IsNullOrWhiteSpace(definition.DisplayName)
            ? item.LootId.Value : definition.DisplayName;
}
