/// <summary>
/// Immutable domain request to move a complete loot quantity between two entities.
/// Optional source Inventory metadata binds extraction to one observed physical slot.
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

    public bool HasInventorySlotAddress => SourceInventorySlotIndex != -1 || ExpectedInventoryRevision != -1;

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
        IsValidInventorySource(SourceInventorySlotIndex, ExpectedInventoryRevision);

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
        SourceId = sourceId;
        DestinationId = destinationId;
        LootId = lootId;
        RequestedAmount = requestedAmount;
        SimulationTick = simulationTick;
    }
}
