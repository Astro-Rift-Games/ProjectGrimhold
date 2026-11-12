using System;
using System.Collections.Generic;

/// <summary>Physical Town slots. Aggregate inputs receive a location only on placement.</summary>
public static class ProfileInventoryRules
{
    /// <summary>Checks whether two non-equipment stash items are compatible to merge into the same stack per Doc 14 §6.1.</summary>
    public static bool CanMerge(StashItem a, StashItem b)
    {
        if (a.LootId != b.LootId) return false;
        if (a.Instance != null || b.Instance != null) return false;
        if (!string.Equals(a.Origin, b.Origin, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.ExpeditionId, b.ExpeditionId, StringComparison.Ordinal)) return false;
        return true;
    }

    /// <summary>Atomic receive: lowest matching slot, otherwise lowest free slot inside capacity.</summary>
    public static bool TryMerge(List<StashItem> destination, IReadOnlyList<StashItem> incoming,
        int slotCapacity = int.MaxValue)
    {
        if (!HasValidSlots(destination) || incoming == null || slotCapacity <= 0) return false;
        var next = new List<StashItem>(destination);
        foreach (StashItem item in incoming)
        {
            if (!item.IsValid) return false;
            int index = -1;
            if (item.Instance == null)
            {
                for (int i = 0; i < next.Count; i++)
                {
                    if (CanMerge(next[i], item))
                    {
                        if (index < 0 || next[i].Slot < next[index].Slot)
                        {
                            index = i;
                        }
                    }
                }
            }
            if (index >= 0)
            {
                StashItem existing = next[index];
                if (existing.Slot >= slotCapacity || existing.Amount > int.MaxValue - item.Amount) return false;
                next[index] = new StashItem(existing.LootId, existing.Amount + item.Amount, existing.Slot, existing.Instance, existing.ExpeditionId, existing.Origin);
            }
            else
            {
                if (next.Count >= slotCapacity) return false;
                int slot = LowestFreeSlot(next);
                if (slot >= slotCapacity) return false;
                next.Add(item.AtSlot(slot));
            }
        }
        next.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        destination.Clear();
        destination.AddRange(next);
        return true;
    }

    /// <summary>Definition-wide consumption drains stacks in ascending slot order, atomically.</summary>
    public static bool TryRemove(List<StashItem> items, LootId lootId, int amount, string expeditionId = null, string origin = null)
    {
        if (!HasValidSlots(items) || amount <= 0 || FindAmount(items, lootId, expeditionId, origin) < amount) return false;
        while (amount > 0)
        {
            int index = -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].LootId == lootId &&
                    (expeditionId == null || string.Equals(items[i].ExpeditionId, expeditionId, StringComparison.Ordinal)) &&
                    (origin == null || string.Equals(items[i].Origin, origin, StringComparison.Ordinal)))
                {
                    if (index < 0 || items[i].Slot < items[index].Slot)
                    {
                        index = i;
                    }
                }
            }
            if (index < 0) return false;
            int removed = Math.Min(amount, items[index].Amount);
            TryRemoveAt(items, items[index].Slot, lootId, removed);
            amount -= removed;
        }
        return true;
    }

    public static bool TryRemoveAt(List<StashItem> items, int slot, LootId lootId, int amount)
    {
        if (!HasValidSlots(items)) return false;
        int index = FindSlotIndex(items, slot);
        if (slot < 0 || index < 0 || amount <= 0 || items[index].LootId != lootId || items[index].Amount < amount) return false;
        StashItem item = items[index];
        if (amount == item.Amount) items.RemoveAt(index);
        else items[index] = new StashItem(lootId, item.Amount - amount, item.Slot, item.Instance, item.ExpeditionId, item.Origin);
        return true;
    }

    public static bool HasValidSlots(IReadOnlyList<StashItem> items)
    {
        if (items == null) return false;
        var slots = new HashSet<int>();
        foreach (StashItem item in items)
            if (!item.IsValid || item.Slot < 0 || !slots.Add(item.Slot)) return false;
        return true;
    }

    public static int FindSlotIndex(IReadOnlyList<StashItem> items, int slot)
    {
        for (int i = 0; i < items.Count; i++) if (items[i].Slot == slot) return i;
        return -1;
    }

    public static int FindIndex(IReadOnlyList<StashItem> items, LootId lootId)
    {
        int result = -1;
        for (int i = 0; i < items.Count; i++)
            if (items[i].LootId == lootId && (result < 0 || items[i].Slot < items[result].Slot)) result = i;
        return result;
    }

    /// <summary>Saturates only the display count; removal still drains the requested exact quantity.</summary>
    public static int FindAmount(IReadOnlyList<StashItem> items, LootId lootId, string expeditionId = null, string origin = null)
    {
        long total = 0;
        foreach (StashItem item in items)
        {
            if (item.LootId == lootId &&
                (expeditionId == null || string.Equals(item.ExpeditionId, expeditionId, StringComparison.Ordinal)) &&
                (origin == null || string.Equals(item.Origin, origin, StringComparison.Ordinal)))
            {
                total += item.Amount;
                if (total >= int.MaxValue) return int.MaxValue;
            }
        }
        return (int)total;
    }

    public static int LowestFreeSlot(IReadOnlyList<StashItem> items)
    {
        int slot = 0;
        while (FindSlotIndex(items, slot) >= 0) slot++;
        return slot;
    }

    public static int CountNewSlots(IReadOnlyList<StashItem> destination, IReadOnlyList<StashItem> incoming)
    {
        var availableSlots = new List<StashItem>(destination);
        int count = 0;
        foreach (StashItem item in incoming)
        {
            if (item.Instance != null)
            {
                count++;
                availableSlots.Add(item);
                continue;
            }

            int match = -1;
            for (int i = 0; i < availableSlots.Count; i++)
            {
                if (CanMerge(availableSlots[i], item))
                {
                    match = i;
                    break;
                }
            }

            if (match >= 0)
            {
                // Can merge into existing slot
            }
            else
            {
                count++;
                availableSlots.Add(item);
            }
        }
        return count;
    }

    public static bool ExceedsCapacity(IReadOnlyList<StashItem> destination, IReadOnlyList<StashItem> incoming, int slotCapacity)
    {
        if (!HasValidSlots(destination) || incoming == null || slotCapacity <= 0) return true;
        var next = new List<StashItem>(destination);
        // Capacity and reducing-only overflow rules are the same rules used by receive.
        foreach (StashItem item in incoming)
        {
            int index = -1;
            if (item.Instance == null)
            {
                for (int i = 0; i < next.Count; i++)
                {
                    if (CanMerge(next[i], item))
                    {
                        if (index < 0 || next[i].Slot < next[index].Slot)
                        {
                            index = i;
                        }
                    }
                }
            }
            if (index >= 0)
            {
                if (next[index].Slot >= slotCapacity) return true;
            }
            else
            {
                if (next.Count >= slotCapacity) return true;
                int slot = LowestFreeSlot(next);
                if (slot >= slotCapacity) return true;
                next.Add(item.AtSlot(slot));
            }
        }
        return false;
    }

    /// <summary>Atomic whole-entry move, total merge or swap. Overflow is never a destination.</summary>
    public static bool TryMove(List<StashItem> source, int fromSlot, List<StashItem> target, int toSlot,
        LootId guard, int sourceCapacity, int targetCapacity)
    {
        if (!HasValidSlots(source) || !HasValidSlots(target)) return false;
        int from = FindSlotIndex(source, fromSlot);
        if (fromSlot < 0 || toSlot < 0 || toSlot >= targetCapacity || from < 0 || source[from].LootId != guard) return false;
        if (ReferenceEquals(source, target) && fromSlot == toSlot) return true;
        int to = FindSlotIndex(target, toSlot);
        StashItem moving = source[from];
        if (to < 0)
        {
            if (!ReferenceEquals(source, target) && target.Count >= targetCapacity) return false;
            source.RemoveAt(from);
            target.Add(moving.AtSlot(toSlot));
        }
        else if (target[to].LootId == guard && moving.Instance == null && target[to].Instance == null && CanMerge(target[to], moving))
        {
            StashItem receiving = target[to];
            if (receiving.Amount > int.MaxValue - moving.Amount) return false;
            target[to] = new StashItem(receiving.LootId, receiving.Amount + moving.Amount, receiving.Slot, receiving.Instance, receiving.ExpeditionId, receiving.Origin);
            source.RemoveAt(from);
        }
        else
        {
            if (fromSlot >= sourceCapacity) return false;
            StashItem displaced = target[to];
            source[from] = displaced.AtSlot(fromSlot);
            target[to] = moving.AtSlot(toSlot);
        }
        source.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        if (!ReferenceEquals(source, target)) target.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        return true;
    }

    /// <summary>Raid boundary: preserve ID-free equipment units; fold other loot in physical-slot order.</summary>
    public static bool TryAggregate(IReadOnlyList<StashItem> items, out List<LootEntry> entries)
    {
        entries = new List<LootEntry>();
        if (items == null) return false;
        var ordered = new List<StashItem>(items);
        ordered.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        var indices = new Dictionary<LootId, int>();
        foreach (StashItem item in ordered)
        {
            if (!item.IsValid) return false;
            if (item.Instance != null)
            {
                entries.Add(new LootEntry(item.LootId, 1, item.Instance.Modifiers, isEquipmentUnit: true));
                continue;
            }
            if (indices.TryGetValue(item.LootId, out int index))
            {
                if (entries[index].Amount > int.MaxValue - item.Amount) return false;
                entries[index] = new LootEntry(item.LootId, entries[index].Amount + item.Amount);
            }
            else
            {
                indices.Add(item.LootId, entries.Count);
                entries.Add(new LootEntry(item.LootId, item.Amount));
            }
        }
        return true;
    }

    /// <summary>Reservation rollback never merges or drops units, including legacy overflow.</summary>
    public static void RestoreSlots(List<StashItem> destination, IReadOnlyList<StashItem> reserved)
    {
        var ordered = new List<StashItem>(reserved);
        ordered.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        foreach (StashItem item in ordered)
        {
            int slot = item.Slot >= 0 && FindSlotIndex(destination, item.Slot) < 0
                ? item.Slot : LowestFreeSlot(destination);
            destination.Add(item.AtSlot(slot));
        }
        destination.Sort((a, b) => a.Slot.CompareTo(b.Slot));
    }
}
