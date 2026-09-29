using System.Collections.Generic;

/// <summary>
/// Pure stack merge, removal and slot-capacity rules of the character aggregate's item lists.
/// Shared by <see cref="LocalProfileStore"/> transactions and the Commerce preview so both
/// evaluate the same final state. One slot holds every unit of one <see cref="LootId"/>.
/// </summary>
public static class ProfileInventoryRules
{
    /// <summary>
    /// Adds every incoming stack, merging into an existing stack of the same loot. Returns false
    /// when a merged amount would overflow; the destination may then be partially updated, so
    /// callers apply it only to a working copy.
    /// </summary>
    public static bool TryMerge(List<StashItem> destination, IReadOnlyList<StashItem> incoming)
    {
        foreach (StashItem item in incoming)
        {
            int index = FindIndex(destination, item.LootId);
            if (index < 0) destination.Add(item);
            else if (destination[index].Amount > int.MaxValue - item.Amount) return false;
            else destination[index] = new StashItem(item.LootId, destination[index].Amount + item.Amount);
        }
        return true;
    }

    /// <summary>
    /// Removes units from one stack and frees its slot when none remain.
    /// </summary>
    public static bool TryRemove(List<StashItem> items, LootId lootId, int amount)
    {
        int index = FindIndex(items, lootId);
        if (index < 0 || items[index].Amount < amount) return false;
        int remaining = items[index].Amount - amount;
        if (remaining == 0) items.RemoveAt(index);
        else items[index] = new StashItem(lootId, remaining);
        return true;
    }

    public static int FindIndex(IReadOnlyList<StashItem> items, LootId lootId)
    {
        for (int i = 0; i < items.Count; i++) if (items[i].LootId == lootId) return i;
        return -1;
    }

    public static int FindAmount(IReadOnlyList<StashItem> items, LootId lootId)
    {
        int index = FindIndex(items, lootId);
        return index >= 0 ? items[index].Amount : 0;
    }

    public static int CountNewSlots(IReadOnlyList<StashItem> destination, IReadOnlyList<StashItem> incoming)
    {
        int result = 0;
        foreach (StashItem item in incoming) if (FindIndex(destination, item.LootId) < 0) result++;
        return result;
    }

    /// <summary>
    /// Whether merging the incoming stacks would occupy more slots than the capacity allows.
    /// </summary>
    public static bool ExceedsCapacity(IReadOnlyList<StashItem> destination, IReadOnlyList<StashItem> incoming, int slotCapacity) =>
        destination.Count + CountNewSlots(destination, incoming) > slotCapacity;
}
