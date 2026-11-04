using System;

namespace Grimhold.Backend
{
    /// <summary>C7: atomic move, whole-stack merge or swap, guarded by source definition.</summary>
    [Serializable]
    public sealed class InventorySlotMoveRequest
    {
        public string fromList;
        public int fromSlot;
        public string toList;
        public int toSlot;
        public string lootId;
        public int expectedRevision;
    }
}
