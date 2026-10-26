using System;
using System.Collections.Generic;

/// <summary>Shared category/name comparer for explicit Sort intentions.</summary>
public static class StashDisplayOrder
{
    public static List<StashItem> SortedCopy(IReadOnlyList<StashItem> source, LootDefinitionCatalog catalog)
    {
        var copy = source == null ? new List<StashItem>() : new List<StashItem>(source);
        copy.Sort((left, right) => Compare(left, right, catalog));
        return copy;
    }

    public static int Compare(StashItem left, StashItem right, LootDefinitionCatalog catalog)
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
        return category >= (int)LootCategory.Resource && category <= (int)LootCategory.Shield
            ? category : int.MaxValue;
    }

    private static string Name(StashItem item, LootDefinition definition) =>
        definition == null || string.IsNullOrWhiteSpace(definition.DisplayName)
            ? item.LootId.Value : definition.DisplayName;
}
