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
    // Nullable backing keeps default(struct) unaddressed without offsetting opaque revision tokens.
    private readonly int? _sourceInventorySlotIndex;
    private readonly int? _expectedInventoryRevision;
    public int SourceInventorySlotIndex => _sourceInventorySlotIndex ?? -1;
    public int ExpectedInventoryRevision => _expectedInventoryRevision ?? -1;
    public RaidLootOrigin? Origin { get; }
    public bool HasInventorySlotAddress => SourceInventorySlotIndex != -1 || ExpectedInventoryRevision != -1;

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
        bool hasWeaponDefinition,
        int sourceInventorySlotIndex = -1,
        int expectedInventoryRevision = -1,
        RaidLootOrigin? origin = null)
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
        _sourceInventorySlotIndex = sourceInventorySlotIndex;
        _expectedInventoryRevision = expectedInventoryRevision;
        Origin = origin;
    }

    public static RaidInventorySlotData Empty => default;

    /// <summary>Copies visual text without changing identity, quantity or definition metadata.</summary>
    public RaidInventorySlotData WithDisplayName(string displayName) => new(
        IsOccupied, LootId, Icon, displayName, Amount, UsesFallback, Tooltip,
        Category, WeaponHandedness, HasWeaponDefinition,
        SourceInventorySlotIndex, ExpectedInventoryRevision, Origin);

    public static RaidInventorySlotData EmptyInventorySlot(int index, int revision) =>
        LootTransferRequest.IsValidInventorySlotAddress(index, revision)
            ? new RaidInventorySlotData(false, default, null, null, 0, false, default,
                LootCategory.None, WeaponHandedness.OneHanded, false, index, revision)
            : Empty;

    public static RaidInventorySlotData CreateInventorySlot(
        LootEntry entry, LootDefinition definition, Sprite placeholder,
        int index, int revision, RaidLootOrigin origin)
    {
        if (!entry.IsValid || !origin.IsValid || !LootTransferRequest.IsValidInventorySlotAddress(index, revision))
            return Empty;
        RaidInventorySlotData data = Create(entry, definition, placeholder);
        return new RaidInventorySlotData(data.IsOccupied, data.LootId, data.Icon, data.DisplayName,
            data.Amount, data.UsesFallback, data.Tooltip, data.Category, data.WeaponHandedness,
            data.HasWeaponDefinition, index, revision, origin);
    }

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
