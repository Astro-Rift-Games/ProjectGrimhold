using System;

namespace Grimhold.Backend
{
    /// <summary>C6: exact permutation of occupied physical slots; only Sort compacts.</summary>
    [Serializable]
    public sealed class InventorySlotOrderRequest
    {
        public string list;
        public int[] slotOrder;
        public int expectedRevision;
    }
}
