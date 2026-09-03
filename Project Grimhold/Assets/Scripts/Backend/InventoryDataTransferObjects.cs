using System;

namespace Grimhold.Backend
{
    // ---------------------------------------------------------------------------
    // Inbound DTOs (responses from the backend)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Full inventory snapshot returned by GET /character/me/inventory.
    /// Used at login to hydrate the local Unity state.
    /// </summary>
    [Serializable]
    public struct InventoryData
    {
        public InventoryItemData[]      stash;
        public InventoryItemData[]      loadout;
        public PreparedEquipmentData    preparedEquipment;
        public PendingReservationData   pendingReservation;
        public ExtractionReceiptData    lastAppliedExtractionReceipt;
    }

    [Serializable]
    public struct ExtractionReceiptData
    {
        public string raidId;
        public int resultSequence;
    }

    /// <summary>A single item slot (stash or loadout entry).</summary>
    [Serializable]
    public struct InventoryItemData
    {
        public string lootId;
        public int    amount;
    }

    /// <summary>The six equipment assignment slots on the character loadout.</summary>
    [Serializable]
    public struct PreparedEquipmentData
    {
        public string weaponSlot1;
        public string weaponSlot2;
        public string helmet;
        public string armor;
        public string gloves;
        public string boots;
    }

    /// <summary>
    /// Raid reservation snapshot persisted on the backend.
    /// Null/empty when no reservation is active.
    /// </summary>
    [Serializable]
    public struct PendingReservationData
    {
        public string                reservationId;
        public InventoryItemData[]   items;
        public PreparedEquipmentData preparedEquipment;
    }

    // ---------------------------------------------------------------------------
    // Outbound DTOs (requests to the backend)
    // ---------------------------------------------------------------------------

    /// <summary>Body for move-to-loadout and move-to-stash operations.</summary>
    [Serializable]
    public struct MoveItemRequest
    {
        public string lootId;
        public int    amount;
    }

    /// <summary>
    /// Body for PUT /character/me/inventory/prepared-equipment.
    /// All slots are optional; omit or leave empty to unassign.
    /// </summary>
    [Serializable]
    public struct UpdatePreparedEquipmentRequest
    {
        public string weaponSlot1;
        public string weaponSlot2;
        public string helmet;
        public string armor;
        public string gloves;
        public string boots;
    }

    /// <summary>Body for POST /character/me/inventory/reservation.</summary>
    [Serializable]
    public struct SaveReservationRequest
    {
        public string               reservationId;
        public InventoryItemData[]  items;
        public PreparedEquipmentData preparedEquipment;
    }

    // ---------------------------------------------------------------------------
    // Move operation result (stash + loadout after mutation)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Response shape returned by move-to-loadout and move-to-stash endpoints.
    /// Contains the updated stash and loadout arrays after the move.
    /// </summary>
    [Serializable]
    public struct MoveItemResult
    {
        public InventoryItemData[] stash;
        public InventoryItemData[] loadout;
    }

    /// <summary>Response shape returned by the update-prepared-equipment endpoint.</summary>
    [Serializable]
    public struct UpdatePreparedEquipmentResult
    {
        public PreparedEquipmentData preparedEquipment;
    }

    /// <summary>Response shape returned by the save-reservation endpoint.</summary>
    [Serializable]
    public struct SaveReservationResult
    {
        public PendingReservationData pendingReservation;
    }

    // ---------------------------------------------------------------------------
    // Extraction loot commit
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Body for POST /character/me/inventory/extraction.
    /// Sent after a successful raid extraction to persist loot to the backend loadout.
    /// Idempotent: replaying the same (raidId, resultSequence) returns alreadySecured = true.
    /// </summary>
    [Serializable]
    public struct CommitExtractionRequest
    {
        public string              raidId;
        public int                 resultSequence;
        public InventoryItemData[] items;
    }

    /// <summary>Response shape returned by the commit-extraction endpoint.</summary>
    [Serializable]
    public struct CommitExtractionResult
    {
        /// <summary>The updated loadout after the extraction was applied.</summary>
        public InventoryItemData[] loadout;
        /// <summary>
        /// True if the (raidId, resultSequence) pair was already recorded on the backend.
        /// The caller should treat this as success — no retry needed.
        /// </summary>
        public bool alreadySecured;
    }

    [Serializable]
    public struct ExtractionProgressionData
    {
        public long consolidatedExperience;
        public int resultingLevel;
    }

    [Serializable]
    public struct CommitExtractionUnifiedRequest
    {
        public string              raidId;
        public int                 resultSequence;
        public InventoryItemData[] items;
        // Progression is optional in backend, so we pass it when XP > 0
        public ExtractionProgressionData progression;
    }

    [Serializable]
    public struct CommitExtractionUnifiedResult
    {
        public bool                  alreadySecured;
        public InventoryItemData[]   loadout;
        public int                   level;
        public long                  experience;
        public CharacterAttributesData characterAttributes;
    }
}
