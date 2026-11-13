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
        public int inventorySchemaVersion;
        public BuybackData              buyback;
        public long                     currency;
        public InventoryItemData[]      stash;
        public InventoryItemData[]      loadout;
        public PreparedEquipmentData    preparedEquipment;
        // The wire uses null for absent optional records; local consumers use default structs.
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public PendingReservationData   pendingReservation;
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public ExtractionReceiptData    lastAppliedExtractionReceipt;
        public int                      revision;
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
        public int slot;
        public string lootId;
        public int    amount;
        public EquipmentInstanceData instance;
    }

    /// <summary>The equipment assignment slots on the character loadout.</summary>
    [Serializable]
    [Newtonsoft.Json.JsonConverter(typeof(PreparedEquipmentJsonConverter))]
    public struct PreparedEquipmentData
    {
        public string weaponSlot1;
        public string weaponSlot2;
        public string helmet;
        public string armor;
        public string gloves;
        public string boots;
        public string offHand1;
        public string offHand2;

        [NonSerialized] public System.Collections.Generic.Dictionary<EquipmentSlot, EquipmentInstanceData> instances;

        public string Get(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.WeaponSetAMainHand => weaponSlot1, EquipmentSlot.WeaponSetBMainHand => weaponSlot2,
            EquipmentSlot.WeaponSetAOffHand => offHand1, EquipmentSlot.WeaponSetBOffHand => offHand2,
            EquipmentSlot.Helmet => helmet, EquipmentSlot.Armor => armor, EquipmentSlot.Gloves => gloves, EquipmentSlot.Boots => boots,
            _ => null
        };

        public void Set(EquipmentSlot slot, string lootId, EquipmentInstanceData instance = null, bool instanceFormat = false)
        {
            switch (slot)
            {
                case EquipmentSlot.WeaponSetAMainHand: weaponSlot1 = lootId; break;
                case EquipmentSlot.WeaponSetBMainHand: weaponSlot2 = lootId; break;
                case EquipmentSlot.WeaponSetAOffHand: offHand1 = lootId; break;
                case EquipmentSlot.WeaponSetBOffHand: offHand2 = lootId; break;
                case EquipmentSlot.Helmet: helmet = lootId; break;
                case EquipmentSlot.Armor: armor = lootId; break;
                case EquipmentSlot.Gloves: gloves = lootId; break;
                case EquipmentSlot.Boots: boots = lootId; break;
            }
            if (instanceFormat)
            {
                instances ??= new System.Collections.Generic.Dictionary<EquipmentSlot, EquipmentInstanceData>();
                instances[slot] = instance;
            }
        }

        public static PreparedEquipmentData FromLoadout(PreparedEquipmentLoadout loadout)
        {
            var data = new PreparedEquipmentData();
            foreach (var slot in EquipmentSlotRules.AllSlots)
                data.Set(slot, loadout.Get(slot).Value, EquipmentInstanceData.FromInstance(loadout.GetInstance(slot)), true);
            return data;
        }

        public bool TryToLoadout(LootDefinitionCatalog catalog, bool requireIdentity, out PreparedEquipmentLoadout loadout, out string error)
        {
            loadout = default;
            error = null;
            foreach (var slot in EquipmentSlotRules.AllSlots)
            {
                string id = Get(slot);
                if (string.IsNullOrEmpty(id)) continue;
                if (catalog == null || !catalog.TryGet(id, out var definition)) { error = "Unknown prepared equipment."; return false; }
                EquipmentInstance instance = null;
                EquipmentInstanceData data = null;
                instances?.TryGetValue(slot, out data);
                if (data != null && !data.TryToInstance(definition, requireIdentity, out instance, out error, catalog)) return false;
                if (requireIdentity && instance == null) { error = "Prepared equipment requires an instance."; return false; }
                loadout = loadout.With(slot, new LootId(id), instance);
            }
            return PreparedEquipmentLoadout.TryValidate(loadout, catalog, false, out error);
        }
    }

    /// <summary>
    /// Raid reservation snapshot persisted on the backend.
    /// Null/empty when no reservation is active.
    /// </summary>
    [Serializable]
    public struct PendingReservationData
    {
        public string                reservationId;
        public string                admittedAt;
        public InventoryItemData[]   items;
        public PreparedEquipmentData preparedEquipment;
    }

    // ---------------------------------------------------------------------------
    // Outbound DTOs (requests to the backend)
    // ---------------------------------------------------------------------------

    [Serializable]
    public struct TrainingWeaponResult
    {
        public PreparedEquipmentData preparedEquipment;
        public InventoryItemData[] stash;
        public InventoryItemData[] loadout;
        public int revision;
    }

    /// <summary>Training grants contain intentions only; instance IDs are issued by the backend.</summary>
    [Serializable]
    public struct TrainingWeaponRequest
    {
        public string weaponId;
        public string destinationSlot;
        public int expectedRevision;
    }

    /// <summary>Body for move-to-loadout and move-to-stash operations.</summary>
    [Serializable]
    public struct MoveItemRequest
    {
        public int sourceSlot;
        public string lootId;
        public int    amount;
        public int    expectedRevision;
    }

    /// <summary>
    /// Body for PUT /character/me/inventory/prepared-equipment.
    /// All slots are optional; omit or leave empty to unassign.
    /// </summary>
    [Serializable]
    public struct UpdatePreparedEquipmentRequest
    {
        [NonSerialized] public PreparedEquipmentData? instanceEquipment;
        public string weaponSlot1;
        public string weaponSlot2;
        public string helmet;
        public string armor;
        public string gloves;
        public string boots;
        public string offHand1;
        public string offHand2;
        public int    expectedRevision;
    }

    /// <summary>Body for POST /character/me/inventory/reservation.</summary>
    [Serializable]
    public struct SaveReservationRequest
    {
        public string reservationId;
    }

    // ---------------------------------------------------------------------------
    // Shop operations
    // ---------------------------------------------------------------------------

    /// <summary>One atomic normal trade; prices are resolved by the backend.</summary>
    [Serializable]
    public struct ShopTradeRequest
    {
        public string transactionId;
        public int expectedRevision;
        public ShopTradePurchaseLine[] purchases;
        public ShopTradeSaleLine[] sales;
    }

    [Serializable]
    public struct ShopTradePurchaseLine
    {
        public string lootId;
        public int amount;
    }

    [Serializable]
    public struct ShopTradeSaleLine
    {
        public string lootId;
        public int amount;
        public int sourceSlot;
    }

    [Serializable]
    public struct ShopSellRequest
    {
        public int sourceSlot;
        public string transactionId;
        public string lootId;
        public int    amount;
        public int    expectedRevision;
    }

    [Serializable]
    public struct ShopBuyRequest
    {
        public string transactionId;
        public string lootId;
        public int    amount;
        public int    expectedRevision;
    }

    [Serializable]
    public struct BuybackEntryData
    {
        public string entryId;
        public string lootId;
        public int amount;
        public long unitPrice;
    }

    [Serializable]
    public struct BuybackData
    {
        public BuybackEntryData[] entries;
    }

    [Serializable]
    public struct ShopBuybackRequest
    {
        public string transactionId;
        public string entryId;
        public int amount;
        public int expectedRevision;
    }

    [Serializable]
    public struct ShopTransactionResult
    {
        public BuybackData buyback;
        public bool alreadySecured;
        public long                  currency;
        public InventoryItemData[]   stash;
        public InventoryItemData[]   loadout;
        public PreparedEquipmentData preparedEquipment;
        public int                   revision;
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
        public int                 revision;
    }

    /// <summary>Response shape returned by the update-prepared-equipment endpoint.</summary>
    [Serializable]
    public struct UpdatePreparedEquipmentResult
    {
        public PreparedEquipmentData preparedEquipment;
        public int                   revision;
    }

    /// <summary>Response shape returned by the save-reservation endpoint.</summary>
    [Serializable]
    public struct SaveReservationResult
    {
        public PendingReservationData pendingReservation;
        public int                    revision;
    }

    [Serializable]
    public struct ClearPendingReservationResult
    {
        public int revision;
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
        public int  revision;
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
        public PreparedEquipmentData preparedEquipment;
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
        public int                   revision;
    }

    [Serializable]
    public struct CommitDefeatUnifiedRequest
    {
        public string raidId;
        public int    resultSequence;
    }

    [Serializable]
    public struct CommitDefeatUnifiedResult
    {
        public bool                    alreadySecured;
        public int                     level;
        public long                    experience;
        public CharacterAttributesData characterAttributes;
        public int                     revision;
    }

    [Serializable]
    public struct PublishExtractionResultRequest
    {
        public string raidId;
        public string resultId;
        public int resultSequence;
        public string targetCharacterId;
        public InventoryItemData[] items;
        public PreparedEquipmentData preparedEquipment;
        public long experienceGranted;
        public string issuedAt;
        public string hostSignature;
    }

    [Serializable]
    public struct PublishExtractionResultResponse
    {
        public string status;
    }
}
