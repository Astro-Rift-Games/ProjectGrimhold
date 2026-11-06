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
    /// <summary>Town equipment instance, or an ID-less Raid weapon unit carrying its modifiers.</summary>
    public EquipmentInstance Instance { get; }
    // Nullable backing keeps default(struct) unaddressed without offsetting opaque revision tokens.
    private readonly int? _sourceInventorySlotIndex;
    private readonly int? _expectedInventoryRevision;
    public int SourceInventorySlotIndex => _sourceInventorySlotIndex ?? -1;
    public int ExpectedInventoryRevision => _expectedInventoryRevision ?? -1;
    public RaidLootOrigin? Origin { get; }
    public bool HasInventorySlotAddress => SourceInventorySlotIndex != -1 || ExpectedInventoryRevision != -1;
    // Container weapon unit address, guarded by the unit's observed modifiers.
    private readonly int? _sourceUnitIndex;
    public int SourceUnitIndex => _sourceUnitIndex ?? -1;
    public WeaponInstanceModifiersNetworkData SourceUnitModifiers { get; }
    public bool HasSourceUnitAddress => SourceUnitIndex != -1;

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
        RaidLootOrigin? origin = null,
        EquipmentInstance instance = null,
        int sourceUnitIndex = -1,
        WeaponInstanceModifiersNetworkData sourceUnitModifiers = default)
    {
        _sourceUnitIndex = sourceUnitIndex;
        SourceUnitModifiers = sourceUnitModifiers;
        IsOccupied = isOccupied;
        LootId = lootId;
        Icon = icon;
        DisplayName = displayName;
        Amount = amount;
        UsesFallback = usesFallback;
        Tooltip = tooltip;
        Instance = instance;
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
        SourceInventorySlotIndex, ExpectedInventoryRevision, Origin, Instance, SourceUnitIndex, SourceUnitModifiers);

    public static RaidInventorySlotData EmptyInventorySlot(int index, int revision) =>
        LootTransferRequest.IsValidInventorySlotAddress(index, revision)
            ? new RaidInventorySlotData(false, default, null, null, 0, false, default,
                LootCategory.None, WeaponHandedness.OneHanded, false, index, revision)
            : Empty;

    public static RaidInventorySlotData CreateInventorySlot(
        LootEntry entry, LootDefinition definition, Sprite placeholder,
        int index, int revision, RaidLootOrigin origin,
        WeaponInstanceModifiersNetworkData modifiers = default,
        CharacterAttributeState? attributes = null, LootDefinitionCatalog catalog = null)
    {
        if (!entry.IsValid || !origin.IsValid || !LootTransferRequest.IsValidInventorySlotAddress(index, revision))
            return Empty;
        RaidInventorySlotData data = Create(entry, definition, placeholder,
            ResolveRaidInstance(definition, modifiers, catalog), attributes, catalog);
        return new RaidInventorySlotData(data.IsOccupied, data.LootId, data.Icon, data.DisplayName,
            data.Amount, data.UsesFallback, data.Tooltip, data.Category, data.WeaponHandedness,
            data.HasWeaponDefinition, index, revision, origin, data.Instance);
    }

    /// <summary>
    /// One container weapon unit cell (ItemInstanceArchitecture section 6.3), addressed by its unit
    /// position and observed modifiers. A modified unit shows its own instance tooltip.
    /// </summary>
    public static RaidInventorySlotData CreateContainerUnit(
        LootDefinition definition, Sprite placeholder, int unitIndex, WeaponInstanceModifiersNetworkData modifiers,
        CharacterAttributeState? attributes, LootDefinitionCatalog catalog)
    {
        if (definition == null || !LootTransferRequest.IsValidSourceUnitAddress(unitIndex)) return Empty;
        RaidInventorySlotData data = Create(new LootEntry(definition.LootId, 1), definition, placeholder,
            ResolveRaidInstance(definition, modifiers, catalog), attributes, catalog);
        return new RaidInventorySlotData(data.IsOccupied, data.LootId, data.Icon, data.DisplayName,
            data.Amount, data.UsesFallback, data.Tooltip, data.Category, data.WeaponHandedness,
            data.HasWeaponDefinition, instance: data.Instance, sourceUnitIndex: unitIndex,
            sourceUnitModifiers: modifiers);
    }

    /// <summary>Plain units keep the definition tooltip; modified units decode an ID-less instance.</summary>
    private static EquipmentInstance ResolveRaidInstance(LootDefinition definition,
        WeaponInstanceModifiersNetworkData modifiers, LootDefinitionCatalog catalog)
    {
        if (modifiers.IsEmpty || definition?.WeaponDefinition == null || catalog == null ||
            !modifiers.TryResolve(catalog, definition.WeaponDefinition.NaturalScalingAttribute,
                out WeaponInstanceModifiers decoded, out _))
            return null;
        return new EquipmentInstance(null, decoded);
    }

    public static RaidInventorySlotData Create(
        LootEntry entry,
        LootDefinition definition,
        Sprite placeholder,
        EquipmentInstance instance = null,
        CharacterAttributeState? attributes = null,
        LootDefinitionCatalog catalog = null)
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
            EquipmentTooltipPresentationBuilder.Build(definition, instance, attributes, catalog),
            definitionMissing ? LootCategory.None : definition.Category,
            definition?.WeaponDefinition?.Handedness ?? WeaponHandedness.OneHanded,
            definition?.WeaponDefinition != null,
            instance: instance);
    }
}
