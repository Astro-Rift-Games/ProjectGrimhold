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
    public EquipmentTooltipPresentation Tooltip { get; }

    /// <summary>Catalog classification of the unit, or None when its definition is unresolved.</summary>
    public LootCategory Category { get; }
    public WeaponHandedness WeaponHandedness { get; }
    public bool HasWeaponDefinition { get; }

    private RaidInventorySlotData(
        bool isOccupied,
        LootId lootId,
        Sprite icon,
        string displayName,
        int amount,
        bool usesFallback,
        EquipmentTooltipPresentation tooltip,
        LootCategory category,
        WeaponHandedness weaponHandedness,
        bool hasWeaponDefinition)
    {
        IsOccupied = isOccupied;
        LootId = lootId;
        Icon = icon;
        DisplayName = displayName;
        Amount = amount;
        UsesFallback = usesFallback;
        Tooltip = tooltip;
        Category = category;
        WeaponHandedness = weaponHandedness;
        HasWeaponDefinition = hasWeaponDefinition;
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
            EquipmentTooltipPresentationBuilder.Build(definition),
            definitionMissing ? LootCategory.None : definition.Category,
            definition?.WeaponDefinition?.Handedness ?? WeaponHandedness.OneHanded,
            definition?.WeaponDefinition != null);
    }
}
