using System.Collections.Generic;
using System.Threading.Tasks;
using Grimhold.Backend;

/// <summary>Protocol-2 Town operations; production implements both this and the compatibility API.</summary>
public interface ISlotRemoteInventoryService
{
    Task<(bool success, BackendError error)> SplitSlotAsync(InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId lootId);
    Task<(bool success, BackendError error)> OrderSlotsAsync(InventoryList list, IReadOnlyList<int> slots);
    Task<(bool success, BackendError error)> TransferAsync(int sourceSlot, LootId id, int amount, bool fromStash);
    Task<(bool success, BackendError error)> MoveSlotsAsync(InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id);
}
