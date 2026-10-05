using System.Collections.Generic;

/// <summary>Stable ids of the tabs the Town player menu can host, in display order.</summary>
public static class TownMenuTabIds
{
    public const string Inventory = "inventory";
    public const string Attributes = "attributes";

    private static readonly string[] OrderedIds = { Inventory, Attributes };

    public static IReadOnlyList<string> All => OrderedIds;
}
