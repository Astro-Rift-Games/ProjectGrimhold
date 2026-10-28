/// <summary>
/// Read capability for gameplay slot capacity and current occupancy.
/// Player Inventory counts physical stacks (including exact-origin subdivisions).
/// Aggregate containers count distinct positive loot identifiers. The owning endpoint
/// defines the storage model; this capability does not imply an aggregate snapshot's row count.
/// </summary>
public interface ILootSlotCapacityReader : IEntity
{
    int SlotCapacity { get; }
    int OccupiedSlotCount { get; }
}
