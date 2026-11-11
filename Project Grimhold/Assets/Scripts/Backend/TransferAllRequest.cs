using System;

namespace Grimhold.Backend
{
    /// <summary>Take All / Leave All: moves every source stack in one revision-guarded request.</summary>
    [Serializable]
    public sealed class TransferAllRequest
    {
        public string direction;
        public int expectedRevision;
    }

    /// <summary>One source stack's outcome; refused stacks stay in the source and carry the backend error code.</summary>
    [Serializable]
    public struct TransferAllStackResult
    {
        public int slot;
        public string lootId;
        public int amount;
        public bool moved;
        public string errorCode;
    }

    /// <summary>Authoritative lists and revision after the bulk transfer, plus the per-stack results in source slot order.</summary>
    [Serializable]
    public struct TransferAllResult
    {
        public InventoryItemData[] stash;
        public InventoryItemData[] loadout;
        public int revision;
        public TransferAllStackResult[] results;
    }
}
