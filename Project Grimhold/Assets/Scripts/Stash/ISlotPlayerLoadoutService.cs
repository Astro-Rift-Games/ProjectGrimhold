using System.Collections.Generic;

/// <summary>
/// Physical-slot capability alongside the legacy IPlayerLoadoutService adapter. It does not
/// require unrelated consumers/test doubles of that interface to implement Town slot mutations.
/// </summary>
public interface ISlotPlayerLoadoutService
{
    int InventorySchemaVersion { get; }
    StashOperationResult EvaluateSplit(ProfileId profile, InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId lootId);
    StashOperationResult TrySplit(ProfileId profile, InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId lootId);
    StashOperationResult EvaluateTransfer(ProfileId profile, int sourceSlot, LootId id, int amount, bool fromStash);
    StashOperationResult TryTransfer(ProfileId profile, int sourceSlot, LootId id, int amount, bool fromStash);
    StashOperationResult EvaluateOrderSlots(ProfileId profile, InventoryList list, IReadOnlyList<int> slots);
    StashOperationResult TryOrderSlots(ProfileId profile, InventoryList list, IReadOnlyList<int> slots);
    StashOperationResult EvaluateMove(ProfileId profile, InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id);
    StashOperationResult TryMove(ProfileId profile, InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id);
}
