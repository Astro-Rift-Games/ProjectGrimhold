using System;

namespace Grimhold.Backend
{
    /// <summary>C8: split part of one guarded stack into an empty slot in the same v2 list.</summary>
    [Serializable]
    public sealed class InventorySlotSplitRequest
    {
        public string list;
        public int sourceSlot;
        public int amount;
        public int destinationSlot;
        public string lootId;
        public int expectedRevision;
    }
}
