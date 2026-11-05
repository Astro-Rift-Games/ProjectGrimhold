using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// Read-only Town projection retaining physical metadata for Commerce without changing the
/// shared Inventory/Raid LootEntry presentation contract. Neither view owns persistent state.
/// </summary>
public sealed class TownInventoryProjection : ReadOnlyCollection<LootEntry>
{
    public IReadOnlyList<StashItem> PhysicalItems { get; }
    public CharacterAttributeState? ConfirmedAttributes { get; internal set; }

    public TownInventoryProjection(IList<LootEntry> entries, List<StashItem> physicalItems) : base(entries)
    {
        PhysicalItems = physicalItems.AsReadOnly();
    }
}
