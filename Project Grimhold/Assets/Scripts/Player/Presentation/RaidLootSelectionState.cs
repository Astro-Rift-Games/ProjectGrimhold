using System.Collections.Generic;

/// <summary>
/// Owns a local selection: snapshot-bound address in Raid, item identity for legacy sources.
/// </summary>
public sealed class RaidLootSelectionState
{
    public LootId SelectedLootId { get; private set; }
    public bool HasSelection => SelectedLootId.IsValid;
    public int SelectedInventorySlotIndex { get; private set; } = -1;
    public int ExpectedInventoryRevision { get; private set; } = -1;

    public bool TrySelect(LootId lootId, IReadOnlyList<LootEntry> occupiedEntries)
    {
        if (!Contains(occupiedEntries, lootId))
        {
            return false;
        }

        SelectedLootId = lootId;
        SelectedInventorySlotIndex = -1;
        ExpectedInventoryRevision = -1;
        return true;
    }

    public void Reconcile(IReadOnlyList<LootEntry> occupiedEntries)
    {
        if (HasSelection && !Contains(occupiedEntries, SelectedLootId))
        {
            Clear();
        }
    }

    public bool TrySelect(int index, int revision, IReadOnlyList<RaidInventorySlotData> slots)
    {
        if (!Matches(slots, index, revision)) return false;
        SelectedLootId = slots[index].LootId;
        SelectedInventorySlotIndex = index;
        ExpectedInventoryRevision = revision;
        return true;
    }

    public void Reconcile(IReadOnlyList<RaidInventorySlotData> slots)
    {
        if (HasSelection && (!Matches(slots, SelectedInventorySlotIndex, ExpectedInventoryRevision) ||
            slots[SelectedInventorySlotIndex].LootId != SelectedLootId)) Clear();
    }

    private static bool Matches(IReadOnlyList<RaidInventorySlotData> slots, int index, int revision) =>
        LootTransferRequest.IsValidInventorySlotAddress(index, revision) && slots != null && index < slots.Count &&
        slots[index].IsOccupied && slots[index].LootId.IsValid &&
        slots[index].SourceInventorySlotIndex == index && slots[index].ExpectedInventoryRevision == revision;

    public void Clear()
    {
        SelectedLootId = default;
        SelectedInventorySlotIndex = -1;
        ExpectedInventoryRevision = -1;
    }

    private static bool Contains(IReadOnlyList<LootEntry> entries, LootId lootId)
    {
        if (!lootId.IsValid || entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsValid && entries[i].LootId == lootId)
            {
                return true;
            }
        }

        return false;
    }
}
