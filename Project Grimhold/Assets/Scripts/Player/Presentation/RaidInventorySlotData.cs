using UnityEngine;

/// <summary>
/// Immutable visual data for one raid-inventory slot.
/// It is a disposable presentation value and never owns gameplay state.
/// </summary>
public readonly struct RaidInventorySlotData
{
    public bool IsOccupied { get; }
    public LootId LootId { get; }
    public Sprite Icon { get; }
    public string DisplayName { get; }
    public int Amount { get; }
    public bool UsesFallback { get; }

    /// <summary>Catalog classification of the unit, or None when its definition is unresolved.</summary>
    public LootCategory Category { get; }

    private RaidInventorySlotData(
        bool isOccupied,
        LootId lootId,
        Sprite icon,
        string displayName,
        int amount,
        bool usesFallback,
        LootCategory category)
    {
        IsOccupied = isOccupied;
        LootId = lootId;
        Icon = icon;
        DisplayName = displayName;
        Amount = amount;
        UsesFallback = usesFallback;
        Category = category;
    }

    public static RaidInventorySlotData Empty => default;

    public static RaidInventorySlotData Create(
        LootEntry entry,
        LootDefinition definition,
        Sprite placeholder)
    {
        if (!entry.IsValid)
        {
            return Empty;
        }

        bool definitionMissing = definition == null;
        string displayName = definitionMissing
            ? entry.LootId.Value
            : definition.DisplayName;
        Sprite icon = definitionMissing || definition.Icon == null
            ? placeholder
            : definition.Icon;

        return new RaidInventorySlotData(
            true,
            entry.LootId,
            icon,
            displayName,
            entry.Amount,
            definitionMissing || definition.Icon == null,
            definitionMissing ? LootCategory.None : definition.Category);
    }
}
