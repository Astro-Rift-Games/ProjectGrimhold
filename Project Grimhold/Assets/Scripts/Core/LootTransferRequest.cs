/// <summary>
/// Immutable domain request to move a complete loot quantity between two entities.
/// Optional source Inventory metadata binds extraction to one observed physical slot; an optional
/// source unit address binds a container extraction to one observed weapon unit.
/// </summary>
public readonly struct LootTransferRequest
{
    public EntityId SourceId { get; }
    public EntityId DestinationId { get; }
    public LootId LootId { get; }
    public int RequestedAmount { get; }
    public int SimulationTick { get; }
    public int SourceInventorySlotIndex { get; }
    public int ExpectedInventoryRevision { get; }
    /// <summary>Container weapon unit address, or -1. Guarded by the unit's observed modifiers.</summary>
    public int SourceUnitIndex { get; }
    public WeaponInstanceModifiersNetworkData SourceUnitModifiers { get; }

    public bool HasInventorySlotAddress => SourceInventorySlotIndex != -1 || ExpectedInventoryRevision != -1;
    public bool HasSourceUnitAddress => SourceUnitIndex != -1;

    public static bool IsValidSourceUnitAddress(int unitIndex) =>
        unitIndex >= 0 && unitIndex < NetworkLootContainer.MaxWeaponUnits;

    // Revisions are opaque tokens. Only -1 is reserved for the legacy unaddressed pair.
    public static bool IsValidInventorySlotAddress(int slotIndex, int expectedRevision) =>
        slotIndex >= 0 && slotIndex < LootInventoryRules.PhysicalSlotCount && expectedRevision != -1;

    public static bool IsValidInventorySource(int slotIndex, int expectedRevision) =>
        (slotIndex == -1 && expectedRevision == -1) || IsValidInventorySlotAddress(slotIndex, expectedRevision);

    public bool IsValid =>
        SourceId.Value != 0 &&
        DestinationId.Value != 0 &&
        LootId.IsValid &&
        RequestedAmount > 0 &&
        IsValidInventorySource(SourceInventorySlotIndex, ExpectedInventoryRevision) &&
        (!HasSourceUnitAddress || (IsValidSourceUnitAddress(SourceUnitIndex) && RequestedAmount == 1 &&
            !HasInventorySlotAddress));

    public LootTransferRequest(
        EntityId sourceId,
        EntityId destinationId,
        LootId lootId,
        int requestedAmount,
        int simulationTick)
        : this(sourceId, destinationId, lootId, requestedAmount, simulationTick, -1, -1)
    {
    }

    public LootTransferRequest(
        EntityId sourceId,
        EntityId destinationId,
        LootId lootId,
        int requestedAmount,
        int simulationTick,
        int sourceInventorySlotIndex,
        int expectedInventoryRevision)
    {
        SourceInventorySlotIndex = sourceInventorySlotIndex;
        ExpectedInventoryRevision = expectedInventoryRevision;
        SourceUnitIndex = -1;
        SourceUnitModifiers = default;
        SourceId = sourceId;
        DestinationId = destinationId;
        LootId = lootId;
        RequestedAmount = requestedAmount;
        SimulationTick = simulationTick;
    }

    private LootTransferRequest(
        EntityId sourceId,
        EntityId destinationId,
        LootId lootId,
        int simulationTick,
        int sourceUnitIndex,
        WeaponInstanceModifiersNetworkData sourceUnitModifiers)
        : this(sourceId, destinationId, lootId, 1, simulationTick)
    {
        SourceUnitIndex = sourceUnitIndex;
        SourceUnitModifiers = sourceUnitModifiers;
    }

    /// <summary>One addressed container weapon unit, expected to still carry <paramref name="expectedModifiers"/>.</summary>
    public static LootTransferRequest ForSourceUnit(EntityId sourceId, EntityId destinationId, LootId lootId,
        int simulationTick, int sourceUnitIndex, WeaponInstanceModifiersNetworkData expectedModifiers) =>
        new(sourceId, destinationId, lootId, simulationTick, sourceUnitIndex, expectedModifiers);
}
