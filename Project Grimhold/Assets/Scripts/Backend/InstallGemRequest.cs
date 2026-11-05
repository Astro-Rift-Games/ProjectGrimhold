using System;

namespace Grimhold.Backend
{
    /// <summary>Town-only remote-first request. The caller owns irreversible confirmation, never local gem mutation.</summary>
    [Serializable]
    public struct InstallGemRequest
    {
        public string transactionId;
        public InventoryList weaponList;
        public int weaponSlot;
        public string instanceId;
        public InventoryList gemList;
        public int gemSlot;
        public string gemLootId;
        public int expectedRevision;
    }
}
