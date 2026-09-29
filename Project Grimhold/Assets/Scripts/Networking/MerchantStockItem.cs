using System;
using UnityEngine.Serialization;

/// <summary>
/// Static configuration of one merchant offering. It only seeds the shared, replicated stock when
/// State Authority initializes the merchant; it is never runtime state.
/// </summary>
[Serializable]
public struct MerchantStockItem
{
    /// <summary>Marks an offering whose stock never runs out.</summary>
    public const int UnlimitedQuantity = -1;

    public LootDefinition Item;

    /// <summary>
    /// Units available when the merchant is initialized, shared by every player.
    /// <see cref="UnlimitedQuantity"/> means unlimited; any other value must be zero or positive.
    /// </summary>
    [FormerlySerializedAs("MaxQuantity")]
    public int InitialQuantity;

    public bool IsUnlimited => InitialQuantity == UnlimitedQuantity;
}
