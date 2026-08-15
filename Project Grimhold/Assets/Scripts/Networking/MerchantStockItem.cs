using System;

[Serializable]
public struct MerchantStockItem
{
    public LootDefinition Item;
    /// <summary>
    /// The maximum quantity a single player can purchase from this merchant in a session.
    /// If 0, it means the item is unlimited (or not available, depending on your design, but usually 0 = disabled, or we can use -1 for unlimited).
    /// Let's say > 0 is limited, -1 is unlimited.
    /// </summary>
    public int MaxQuantity;
}
