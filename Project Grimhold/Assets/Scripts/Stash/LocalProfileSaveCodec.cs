using System;
using System.Collections.Generic;
using Grimhold.Backend;
using Newtonsoft.Json;

/// <summary>
/// Encodes and validates the versioned local profile JSON DTOs.
/// </summary>
public static class LocalProfileSaveCodec
{
    [Serializable]
    private sealed class SaveData
    {
        public int schemaVersion;
        public int inventorySchemaVersion;
        public bool requiresEquipmentHydration;
        public PreparedEquipmentData? preparedEquipment;
        public string profileId;
        public long currency;
        public int guildReputation;
        public int level;
        public long currentExperience;
        public int lastAppliedProgressionResultSequence;
        public ProgressionReceiptData lastProgressionReceipt;
        public ProgressionReceiptData[] appliedProgressionReceipts;
        public ItemData[] stash;
        public ItemData[] loadout;
        public string preparedWeaponSlot1;
        public string preparedWeaponSlot2;
        public string preparedWeaponSetAMainHand;
        public string preparedWeaponSetAOffHand;
        public string preparedWeaponSetBMainHand;
        public string preparedWeaponSetBOffHand;
        public string preparedHelmet;
        public string preparedArmor;
        public string preparedGloves;
        public string preparedBoots;
        public ReservationData pendingReservation;
        public PendingExtractionCommitData pendingExtractionCommit;
        public ReceiptData[] appliedExtractionReceipts;
        public long shopIdempotencyWatermark;
        public ShopReceiptData[] appliedShopTransactionReceipts;
        public MissionInstanceStateData[] activeMissions;
        public string[] unlockedAbilityIds;
        public string preparedAbilitySlot1;
        public string preparedAbilitySlot2;
        public string quickSlot1;
        public string quickSlot2;
        public string quickSlot3;
        public string quickSlot4;
        public int vitality;
        public int resistance;
        public int strength;
        public int dexterity;
        public int intelligence;
        public int luck;
        public int availableAttributePoints;
    }

    [Serializable]
    private sealed class ItemData
    {
        public int slot;
        public string lootId;
        public int amount;
        public EquipmentInstanceData instance;
        public string expeditionId;
        public string origin;
    }

    [Serializable]
    private sealed class ReservationData
    {
        public string reservationId;
        public PreparedEquipmentData? preparedEquipment;
        public ItemData[] items;
        public string preparedWeaponSlot1;
        public string preparedWeaponSlot2;
        public string preparedWeaponSetAMainHand;
        public string preparedWeaponSetAOffHand;
        public string preparedWeaponSetBMainHand;
        public string preparedWeaponSetBOffHand;
        public string preparedHelmet;
        public string preparedArmor;
        public string preparedGloves;
        public string preparedBoots;
    }

    [Serializable]
    private sealed class PendingExtractionCommitData
    {
        public string raidId;
        public string profileId;
        public int resultSequence;
        public PreparedEquipmentData? preparedEquipment;
        public ItemData[] items;
        public string preparedWeaponSlot1;
        public string preparedWeaponSlot2;
        public string preparedWeaponSetAMainHand;
        public string preparedWeaponSetAOffHand;
        public string preparedWeaponSetBMainHand;
        public string preparedWeaponSetBOffHand;
        public string preparedHelmet;
        public string preparedArmor;
        public string preparedGloves;
        public string preparedBoots;
        public long consolidatedExperience;
        public int resultingLevel;
    }

    [Serializable]
    private sealed class ReceiptData
    {
        public string raidId;
        public string profileId;
        public int resultSequence;
    }

    [Serializable]
    private sealed class ShopReceiptData
    {
        public long timestamp;
        public string transactionId;
        public string profileId;
    }

    [Serializable]
    private sealed class ProgressionReceiptData
    {
        public string raidId;
        public string profileId;
        public int resultSequence;
        public long consolidatedExperience;
        public int resultingLevel;
    }

    [Serializable]
    private sealed class MissionInstanceStateData
    {
        public string missionId;
        public int state;
        public int currentPhaseIndex;
        public ObjectiveProgressStateData[] objectiveProgress;
    }

    [Serializable]
    private sealed class ObjectiveProgressStateData
    {
        public int index;
        public int currentAmount;
        public string expeditionId;
    }

    public static string Encode(LocalProfileSnapshot snapshot)
    {
        var data = new SaveData
        {
            schemaVersion = LocalProfileSnapshot.CurrentSchemaVersion,
            inventorySchemaVersion = snapshot.InventorySchemaVersion,
            requiresEquipmentHydration = snapshot.RequiresEquipmentHydration,
            preparedEquipment = PreparedEquipmentData.FromLoadout(snapshot.PreparedEquipment),
            profileId = snapshot.ProfileId.Value,
            currency = snapshot.Currency,
            guildReputation = snapshot.GuildReputation,
            level = snapshot.Level,
            currentExperience = snapshot.CurrentExperience,
            lastAppliedProgressionResultSequence = snapshot.LastAppliedProgressionResultSequence,
            lastProgressionReceipt = snapshot.LastProgressionReceipt.HasValue
                ? ToProgressionReceipt(snapshot.LastProgressionReceipt.Value)
                : null,
            appliedProgressionReceipts = ToProgressionReceipts(snapshot.AppliedProgressionReceipts),
            stash = ToItems(snapshot.Stash),
            loadout = ToItems(snapshot.Loadout),
            preparedWeaponSetAMainHand = snapshot.PreparedEquipment.WeaponSetAMainHand.Value,
            preparedWeaponSetAOffHand = snapshot.PreparedEquipment.WeaponSetAOffHand.Value,
            preparedWeaponSetBMainHand = snapshot.PreparedEquipment.WeaponSetBMainHand.Value,
            preparedWeaponSetBOffHand = snapshot.PreparedEquipment.WeaponSetBOffHand.Value,
            preparedHelmet = snapshot.PreparedEquipment.Helmet.Value,
            preparedArmor = snapshot.PreparedEquipment.Armor.Value,
            preparedGloves = snapshot.PreparedEquipment.Gloves.Value,
            preparedBoots = snapshot.PreparedEquipment.Boots.Value,
            pendingReservation = snapshot.PendingReservation == null ? null : new ReservationData
            {
                reservationId = snapshot.PendingReservation.ReservationId,
                preparedEquipment = PreparedEquipmentData.FromLoadout(snapshot.PendingReservation.PreparedEquipment),
                items = ToItems(snapshot.PendingReservation.Items),
                preparedWeaponSetAMainHand = snapshot.PendingReservation.PreparedEquipment.WeaponSetAMainHand.Value,
                preparedWeaponSetAOffHand = snapshot.PendingReservation.PreparedEquipment.WeaponSetAOffHand.Value,
                preparedWeaponSetBMainHand = snapshot.PendingReservation.PreparedEquipment.WeaponSetBMainHand.Value,
                preparedWeaponSetBOffHand = snapshot.PendingReservation.PreparedEquipment.WeaponSetBOffHand.Value,
                preparedHelmet = snapshot.PendingReservation.PreparedEquipment.Helmet.Value,
                preparedArmor = snapshot.PendingReservation.PreparedEquipment.Armor.Value,
                preparedGloves = snapshot.PendingReservation.PreparedEquipment.Gloves.Value,
                preparedBoots = snapshot.PendingReservation.PreparedEquipment.Boots.Value
            },
            pendingExtractionCommit = snapshot.PendingExtractionCommit == null ? null : new PendingExtractionCommitData
            {
                raidId = snapshot.PendingExtractionCommit.Receipt.RaidId,
                preparedEquipment = PreparedEquipmentData.FromLoadout(snapshot.PendingExtractionCommit.PreparedEquipment),
                profileId = snapshot.PendingExtractionCommit.Receipt.ProfileId.Value,
                resultSequence = snapshot.PendingExtractionCommit.Receipt.ResultSequence,
                items = ToItems(snapshot.PendingExtractionCommit.Items),
                preparedWeaponSetAMainHand = snapshot.PendingExtractionCommit.PreparedEquipment.WeaponSetAMainHand.Value,
                preparedWeaponSetAOffHand = snapshot.PendingExtractionCommit.PreparedEquipment.WeaponSetAOffHand.Value,
                preparedWeaponSetBMainHand = snapshot.PendingExtractionCommit.PreparedEquipment.WeaponSetBMainHand.Value,
                preparedWeaponSetBOffHand = snapshot.PendingExtractionCommit.PreparedEquipment.WeaponSetBOffHand.Value,
                preparedHelmet = snapshot.PendingExtractionCommit.PreparedEquipment.Helmet.Value,
                preparedArmor = snapshot.PendingExtractionCommit.PreparedEquipment.Armor.Value,
                preparedGloves = snapshot.PendingExtractionCommit.PreparedEquipment.Gloves.Value,
                preparedBoots = snapshot.PendingExtractionCommit.PreparedEquipment.Boots.Value,
                consolidatedExperience = snapshot.PendingExtractionCommit.ConsolidatedExperience,
                resultingLevel = snapshot.PendingExtractionCommit.ResultingLevel
            },
            appliedExtractionReceipts = ToReceipts(snapshot.AppliedExtractionReceipts),
            shopIdempotencyWatermark = snapshot.ShopIdempotencyWatermark,
            appliedShopTransactionReceipts = ToShopReceipts(snapshot.AppliedShopTransactionReceipts),
            activeMissions = ToMissions(snapshot.ActiveMissions),
            unlockedAbilityIds = ToAbilityIds(snapshot.UnlockedAbilities),
            preparedAbilitySlot1 = snapshot.PreparedAbilities.Slot1.Value ?? string.Empty,
            preparedAbilitySlot2 = snapshot.PreparedAbilities.Slot2.Value ?? string.Empty,
            quickSlot1 = snapshot.QuickSlots.Get(0).Value,
            quickSlot2 = snapshot.QuickSlots.Get(1).Value,
            quickSlot3 = snapshot.QuickSlots.Get(2).Value,
            quickSlot4 = snapshot.QuickSlots.Get(3).Value,
            vitality = snapshot.CharacterAttributes.Vitality,
            resistance = snapshot.CharacterAttributes.Resistance,
            strength = snapshot.CharacterAttributes.Strength,
            dexterity = snapshot.CharacterAttributes.Dexterity,
            intelligence = snapshot.CharacterAttributes.Intelligence,
            luck = snapshot.CharacterAttributes.Luck,
            availableAttributePoints = snapshot.CharacterAttributes.AvailablePoints
        };
        return JsonConvert.SerializeObject(data, Formatting.Indented);
    }

    public static bool TryDecode(
        string json,
        ProfileId expectedProfileId,
        LootDefinitionCatalog catalog,
        out LocalProfileSnapshot snapshot,
        out LocalProfilePersistenceStatus status,
        out string error) =>
        TryDecode(
            json,
            expectedProfileId,
            catalog,
            null,
            out snapshot,
            out status,
            out error);

    public static bool TryDecode(
        string json,
        ProfileId expectedProfileId,
        LootDefinitionCatalog catalog,
        AbilityDefinitionCatalog abilityCatalog,
        out LocalProfileSnapshot snapshot,
        out LocalProfilePersistenceStatus status,
        out string error)
    {
        snapshot = null;
        status = LocalProfilePersistenceStatus.Unavailable;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Profile file is empty.";
            return false;
        }

        SaveData data;
        try
        {
            // Inspect the envelope before interpreting instance fields. Future caches must never
            // become eligible for corrupt-cache reset merely because their item format evolved.
            var document = Newtonsoft.Json.Linq.JObject.Parse(json);
            int version = document.Value<int>("schemaVersion");
            if (version > LocalProfileSnapshot.CurrentSchemaVersion)
            {
                status = LocalProfilePersistenceStatus.UnsupportedVersion;
                error = $"Unsupported profile schema version {version}.";
                return false;
            }
            data = document.ToObject<SaveData>();
        }
        catch (Exception exception)
        {
            error = $"Profile JSON is malformed: {exception.Message}";
            return false;
        }

        if (data == null)
        {
            error = "Profile JSON produced no data.";
            return false;
        }

        if (data.schemaVersion < 1)
        {
            error = $"Invalid profile schema version {data.schemaVersion}.";
            return false;
        }

        if (!expectedProfileId.IsValid || !string.Equals(data.profileId, expectedProfileId.Value, StringComparison.Ordinal))
        {
            error = "Profile ID does not match the local identity.";
            return false;
        }

        var candidate = new LocalProfileSnapshot
        {
            SchemaVersion = LocalProfileSnapshot.CurrentSchemaVersion,
            ProfileId = expectedProfileId,
            InventorySchemaVersion = data.schemaVersion >= 10 ? data.inventorySchemaVersion : 1,
            RequiresEquipmentHydration = data.schemaVersion <= 10 || data.requiresEquipmentHydration
        };
        if (candidate.InventorySchemaVersion < 1 || candidate.InventorySchemaVersion > 3)
        {
            error = "Invalid backend inventory schema version.";
            return false;
        }
        if (!TryReadItems(data.stash, catalog, candidate.Stash, "stash", out error, data.schemaVersion >= 10) ||
            !TryReadItems(data.loadout, catalog, candidate.Loadout, "loadout", out error, data.schemaVersion >= 10))
        {
            return false;
        }

        if (data.schemaVersion >= 6 &&
            !TryReadAbilityIds(data.unlockedAbilityIds, candidate.UnlockedAbilities, out error))
        {
            return false;
        }

        if (data.schemaVersion >= 8)
        {
            if (CharacterAttributeState.TryCreate(
                    data.vitality,
                    data.resistance,
                    data.strength,
                    data.dexterity,
                    data.intelligence,
                    data.luck,
                    data.availableAttributePoints,
                    out CharacterAttributeState attributes))
            {
                candidate.CharacterAttributes = attributes;
            }
            else
            {
                error = "Profile character attributes state is invalid.";
                return false;
            }
        }

        PreparedAbilityLoadout preparedAbilities = default;
        if (data.schemaVersion >= 7 &&
            !TryReadPreparedAbilities(
                data.preparedAbilitySlot1,
                data.preparedAbilitySlot2,
                candidate.UnlockedAbilities,
                candidate.CharacterAttributes,
                abilityCatalog,
                out preparedAbilities,
                out error))
        {
            return false;
        }

        if (data.schemaVersion >= 7)
        {
            candidate.PreparedAbilities = preparedAbilities;
        }

        if (data.schemaVersion >= 9)
        {
            if (!TryReadQuickSlots(data, catalog, out QuickSlotBindings quickSlots, out error))
            {
                return false;
            }

            candidate.QuickSlots = quickSlots;
        }

        // JsonUtility assigns 0L to absent long fields. This is safe while
        // InitialCurrency == 0L. If InitialCurrency changes, this migration
        // path must be made explicit (detect absent field and apply new default).
        if (data.currency < 0)
        {
            error = "Profile currency is negative.";
            return false;
        }
        candidate.Currency = data.currency;
        if (data.schemaVersion >= 10)
        {
            if (data.guildReputation < 0)
            {
                error = "Profile guild reputation is negative.";
                return false;
            }
            candidate.GuildReputation = data.guildReputation;
        }
        else
        {
            candidate.GuildReputation = 0;
        }

        if (data.schemaVersion == 1)
        {
            candidate.Level = ExperienceCurve.InitialLevel;
            candidate.CurrentExperience = 0;
            candidate.LastAppliedProgressionResultSequence = 0;
            candidate.LastProgressionReceipt = null;
        }
        else if (!TryReadProgression(data, expectedProfileId, candidate, out error))
        {
            return false;
        }

        bool hasPendingExtractionData = data.pendingExtractionCommit != null &&
            (!string.IsNullOrWhiteSpace(data.pendingExtractionCommit.raidId) ||
             !string.IsNullOrWhiteSpace(data.pendingExtractionCommit.profileId) ||
             data.pendingExtractionCommit.resultSequence != 0 ||
             (data.pendingExtractionCommit.items != null && data.pendingExtractionCommit.items.Length > 0) ||
             data.pendingExtractionCommit.consolidatedExperience != 0 ||
             data.pendingExtractionCommit.resultingLevel != 0);

        // Loadout slot capacity is a Store rule, not a storage rule: backend data over capacity
        // must stay persistable so operations that reduce the excess can still commit.
        candidate.PreparedEquipment = ReadPreparedEquipment(
            data.schemaVersion >= 4 ? data.preparedWeaponSetAMainHand : data.preparedWeaponSlot1,
            data.schemaVersion >= 4 ? data.preparedWeaponSetBMainHand : data.preparedWeaponSlot2,
            data.preparedHelmet,
            data.preparedArmor,
            data.preparedGloves,
            data.preparedBoots,
            data.schemaVersion >= 4 ? data.preparedWeaponSetAOffHand : null,
            data.schemaVersion >= 4 ? data.preparedWeaponSetBOffHand : null);
        if (data.schemaVersion >= 11 && data.preparedEquipment.HasValue)
        {
            if (!data.preparedEquipment.Value.TryToLoadout(catalog,
                candidate.InventorySchemaVersion == 3 && !candidate.RequiresEquipmentHydration && !hasPendingExtractionData,
                out var prepared, out error)) return false;
            candidate.PreparedEquipment = prepared;
        }
        if (data.schemaVersion < 3)
        {
            TryMigrateLegacyEquipmentOwnership(candidate.Loadout, candidate.PreparedEquipment);
        }
        if (!PreparedEquipmentLoadout.TryValidate(
                candidate.PreparedEquipment,
                catalog,
                requireWeapon: false,
                out error))
        {
            return false;
        }

        // Legacy JsonUtility saves can contain an empty nested DTO for a null
        // reference. Treat that exact empty shape as "no reservation"; any
        // reservation carrying data still requires a valid identity.
        bool hasPendingReservationData = data.pendingReservation != null &&
            (!string.IsNullOrWhiteSpace(data.pendingReservation.reservationId) ||
             (data.pendingReservation.items != null && data.pendingReservation.items.Length > 0) ||
             !string.IsNullOrWhiteSpace(data.pendingReservation.preparedWeaponSlot1) ||
             !string.IsNullOrWhiteSpace(data.pendingReservation.preparedWeaponSlot2) ||
             !string.IsNullOrWhiteSpace(data.pendingReservation.preparedWeaponSetAMainHand) ||
             !string.IsNullOrWhiteSpace(data.pendingReservation.preparedWeaponSetAOffHand) ||
             !string.IsNullOrWhiteSpace(data.pendingReservation.preparedWeaponSetBMainHand) ||
             !string.IsNullOrWhiteSpace(data.pendingReservation.preparedWeaponSetBOffHand) ||
             !string.IsNullOrWhiteSpace(data.pendingReservation.preparedHelmet) ||
             !string.IsNullOrWhiteSpace(data.pendingReservation.preparedArmor) ||
             !string.IsNullOrWhiteSpace(data.pendingReservation.preparedGloves) ||
             !string.IsNullOrWhiteSpace(data.pendingReservation.preparedBoots));
        if (hasPendingReservationData)
        {
            if (string.IsNullOrWhiteSpace(data.pendingReservation.reservationId))
            {
                error = "Pending loadout reservation has no valid reservation ID.";
                return false;
            }

            if (!TryReadItems(data.pendingReservation.items, catalog, out List<StashItem> reservationItems, "reservation", out error, data.schemaVersion >= 10))
            {
                return false;
            }
            PreparedEquipmentLoadout reservedEquipment = ReadPreparedEquipment(
                data.schemaVersion >= 4 ? data.pendingReservation.preparedWeaponSetAMainHand : data.pendingReservation.preparedWeaponSlot1,
                data.schemaVersion >= 4 ? data.pendingReservation.preparedWeaponSetBMainHand : data.pendingReservation.preparedWeaponSlot2,
                data.pendingReservation.preparedHelmet,
                data.pendingReservation.preparedArmor,
                data.pendingReservation.preparedGloves,
                data.pendingReservation.preparedBoots,
                data.schemaVersion >= 4 ? data.pendingReservation.preparedWeaponSetAOffHand : null,
                data.schemaVersion >= 4 ? data.pendingReservation.preparedWeaponSetBOffHand : null);
            if (data.schemaVersion >= 11 && data.pendingReservation.preparedEquipment.HasValue &&
                !data.pendingReservation.preparedEquipment.Value.TryToLoadout(catalog,
                    candidate.InventorySchemaVersion == 3 && !candidate.RequiresEquipmentHydration, out reservedEquipment, out error)) return false;
            if (data.schemaVersion < 3)
            {
                TryMigrateLegacyEquipmentOwnership(reservationItems, reservedEquipment);
            }
            if (!PreparedEquipmentLoadout.TryValidate(
                    reservedEquipment,
                    catalog,
                    requireWeapon: true,
                    out error))
            {
                return false;
            }
            candidate.PendingReservation = new PendingLoadoutReservation(
                data.pendingReservation.reservationId,
                reservationItems,
                reservedEquipment);
        }

        if (hasPendingExtractionData)
        {
            var receipt = new ExtractionReceipt(
                data.pendingExtractionCommit.raidId,
                expectedProfileId,
                data.pendingExtractionCommit.resultSequence);

            if (!receipt.IsValid || !string.Equals(data.pendingExtractionCommit.profileId, expectedProfileId.Value, StringComparison.Ordinal))
            {
                error = "Pending extraction commit contains invalid receipt data.";
                return false;
            }

            if (!TryReadItems(data.pendingExtractionCommit.items, catalog, out List<StashItem> extractionItems, "pending extraction", out error, aggregate: true))
            {
                return false;
            }

            PreparedEquipmentLoadout extractionEquipment = ReadPreparedEquipment(
                data.schemaVersion >= 4 ? data.pendingExtractionCommit.preparedWeaponSetAMainHand : data.pendingExtractionCommit.preparedWeaponSlot1,
                data.schemaVersion >= 4 ? data.pendingExtractionCommit.preparedWeaponSetBMainHand : data.pendingExtractionCommit.preparedWeaponSlot2,
                data.pendingExtractionCommit.preparedHelmet,
                data.pendingExtractionCommit.preparedArmor,
                data.pendingExtractionCommit.preparedGloves,
                data.pendingExtractionCommit.preparedBoots,
                data.schemaVersion >= 4 ? data.pendingExtractionCommit.preparedWeaponSetAOffHand : null,
                data.schemaVersion >= 4 ? data.pendingExtractionCommit.preparedWeaponSetBOffHand : null);

            if (data.schemaVersion >= 11 && data.pendingExtractionCommit.preparedEquipment.HasValue &&
                !data.pendingExtractionCommit.preparedEquipment.Value.TryToLoadout(catalog, false, out extractionEquipment, out error)) return false;
            candidate.PendingExtractionCommit = new PendingExtractionCommit(
                receipt,
                extractionItems,
                extractionEquipment,
                data.pendingExtractionCommit.consolidatedExperience,
                data.pendingExtractionCommit.resultingLevel);
        }

        if (data.appliedExtractionReceipts != null)
        {
            var seen = new HashSet<ExtractionReceipt>();
            foreach (ReceiptData receiptData in data.appliedExtractionReceipts)
            {
                var receipt = new ExtractionReceipt(receiptData.raidId, expectedProfileId, receiptData.resultSequence);
                if (!receipt.IsValid || !string.Equals(receiptData.profileId, expectedProfileId.Value, StringComparison.Ordinal) || !seen.Add(receipt))
                {
                    error = "Applied extraction receipts contain invalid or duplicate data.";
                    return false;
                }
                candidate.AppliedExtractionReceipts.Add(receipt);
            }
        }

        if (candidate.AppliedExtractionReceipts.Count > LocalProfileSnapshot.MaxAppliedExtractionReceipts)
        {
            error = "Applied extraction receipt history exceeds its limit.";
            return false;
        }

        candidate.ShopIdempotencyWatermark = data.shopIdempotencyWatermark;

        if (data.appliedShopTransactionReceipts != null)
        {
            var seen = new HashSet<ShopTransactionReceipt>();
            foreach (ShopReceiptData receiptData in data.appliedShopTransactionReceipts)
            {
                if (receiptData == null || !Guid.TryParse(receiptData.transactionId, out Guid parsedGuid))
                {
                    error = "Applied shop transaction receipts contain invalid transaction ID.";
                    return false;
                }
                var txId = new ShopTransactionId(receiptData.timestamp, parsedGuid);
                var receipt = new ShopTransactionReceipt(txId, expectedProfileId);
                
                if (!receipt.IsValid || !string.Equals(receiptData.profileId, expectedProfileId.Value, StringComparison.Ordinal) || !seen.Add(receipt))
                {
                    error = "Applied shop transaction receipts contain invalid or duplicate data.";
                    return false;
                }

                candidate.AppliedShopTransactionReceipts.Add(receipt);
            }
        }

        if (candidate.AppliedShopTransactionReceipts.Count > LocalProfileSnapshot.MaxAppliedShopTransactionReceipts)
        {
            error = "Applied shop transaction receipt history exceeds its limit.";
            return false;
        }

        if (data.schemaVersion >= 5 && data.activeMissions != null)
        {
            foreach (var missionData in data.activeMissions)
            {
                if (string.IsNullOrWhiteSpace(missionData.missionId))
                {
                    error = "Mission instance data contains an invalid ID.";
                    return false;
                }
                var mission = new MissionInstanceState(new MissionId(missionData.missionId), (MissionState)missionData.state);
                mission.CurrentPhaseIndex = missionData.currentPhaseIndex;
                if (missionData.objectiveProgress != null)
                {
                    foreach (var objData in missionData.objectiveProgress)
                    {
                        mission.ObjectiveProgress[objData.index] = new ObjectiveProgressState(objData.currentAmount, objData.expeditionId);
                    }
                }
                candidate.ActiveMissions.Add(mission);
            }
        }

        if (data.schemaVersion >= 11 && candidate.InventorySchemaVersion == 3 && !candidate.RequiresEquipmentHydration)
        {
            if (!ValidateInstanceItems(candidate.Stash, catalog) ||
                (candidate.PendingExtractionCommit == null &&
                 (!ValidateInstanceItems(candidate.Loadout, catalog) ||
                  !PreparedEquipmentData.FromLoadout(candidate.PreparedEquipment).TryToLoadout(catalog, true, out _, out _))) ||
                (candidate.PendingReservation != null &&
                 (!ValidateInstanceItems(candidate.PendingReservation.Items, catalog) ||
                  !PreparedEquipmentData.FromLoadout(candidate.PendingReservation.PreparedEquipment).TryToLoadout(catalog, true, out _, out _))) ||
                !ApplicationStashServiceBootstrapper.HaveUniqueInstanceIds(candidate.Stash, candidate.Loadout,
                    candidate.PendingReservation?.Items, candidate.PreparedEquipment, candidate.PendingReservation?.PreparedEquipment ?? default))
            { error = "Invalid or duplicate equipment instances."; return false; }
        }
        if (data.schemaVersion <= 10)
        {
            ExpandLegacyEquipment(candidate.Stash, catalog);
            ExpandLegacyEquipment(candidate.Loadout, catalog);
            candidate.PreparedEquipment = PlainCacheEquipment(candidate.PreparedEquipment, catalog);
            if (candidate.PendingReservation != null)
            {
                var items = new List<StashItem>(candidate.PendingReservation.Items);
                ExpandLegacyEquipment(items, catalog);
                candidate.PendingReservation = new PendingLoadoutReservation(candidate.PendingReservation.ReservationId, items,
                    PlainCacheEquipment(candidate.PendingReservation.PreparedEquipment, catalog));
            }
        }
        snapshot = candidate;
        status = LocalProfilePersistenceStatus.Ready;
        return true;
    }

    /// <summary>
    /// Legacy schemas did not encode which of the two historical ownership semantics produced a
    /// save. A complete Inventory reference set is treated as the temporary non-owning representation;
    /// otherwise the all-or-nothing fallback preserves the former exclusive representation.
    /// </summary>
    private static bool TryMigrateLegacyEquipmentOwnership(
        List<StashItem> inventory,
        in PreparedEquipmentLoadout equipment)
    {
        var migrated = new List<StashItem>(inventory);
        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        for (int index = 0; index < slots.Length; index++)
        {
            LootId lootId = equipment.Get(slots[index]);
            if (lootId.IsValid && !TryRemoveOne(migrated, lootId))
            {
                return false;
            }
        }

        inventory.Clear();
        inventory.AddRange(migrated);
        return true;
    }

    private static bool TryRemoveOne(List<StashItem> items, LootId lootId)
    {
        for (int index = 0; index < items.Count; index++)
        {
            StashItem item = items[index];
            if (item.LootId != lootId)
            {
                continue;
            }

            if (item.Amount == 1)
            {
                items.RemoveAt(index);
            }
            else
            {
                items[index] = new StashItem(lootId, item.Amount - 1, item.Slot, item.Instance, item.ExpeditionId, item.Origin);
            }

            return true;
        }

        return false;
    }

    private static bool TryReadProgression(
        SaveData data,
        ProfileId expectedProfileId,
        LocalProfileSnapshot candidate,
        out string error)
    {
        error = null;
        ExperienceCurve curve = ProgressionBalanceDefaults.InitialExperienceCurve;
        if (!CharacterProgressionRules.IsValidState(
                curve,
                data.level,
                data.currentExperience) ||
            data.lastAppliedProgressionResultSequence < 0)
        {
            error = "Profile progression state is invalid.";
            return false;
        }

        candidate.Level = data.level;
        candidate.CurrentExperience = data.currentExperience;
        candidate.LastAppliedProgressionResultSequence =
            data.lastAppliedProgressionResultSequence;

        if (data.lastAppliedProgressionResultSequence == 0)
        {
            if (HasProgressionReceiptData(data.lastProgressionReceipt))
            {
                error = "A zero progression watermark cannot have a last receipt.";
                return false;
            }

            candidate.LastProgressionReceipt = null;
        }
        else
        {
            if (!TryReadProgressionReceipt(
                    data.lastProgressionReceipt,
                    expectedProfileId,
                    out ProgressionReceipt lastReceipt) ||
                lastReceipt.ResultSequence != data.lastAppliedProgressionResultSequence)
            {
                error = "Last receipt does not match the durable progression watermark.";
                return false;
            }

            candidate.LastProgressionReceipt = lastReceipt;
        }

        int previousSequence = 0;
        if (data.appliedProgressionReceipts != null)
        {
            foreach (ProgressionReceiptData receiptData in data.appliedProgressionReceipts)
            {
                if (!TryReadProgressionReceipt(
                        receiptData,
                        expectedProfileId,
                        out ProgressionReceipt receipt) ||
                    receipt.ResultSequence <= previousSequence ||
                    receipt.ResultSequence > data.lastAppliedProgressionResultSequence)
                {
                    error = "Applied progression receipts contain invalid or unordered data.";
                    return false;
                }

                candidate.AppliedProgressionReceipts.Add(receipt);
                previousSequence = receipt.ResultSequence;
            }
        }

        if (candidate.AppliedProgressionReceipts.Count >
            LocalProfileSnapshot.MaxAppliedProgressionReceipts)
        {
            error = "Applied progression receipt history exceeds its limit.";
            return false;
        }

        if (candidate.LastProgressionReceipt.HasValue &&
            (candidate.AppliedProgressionReceipts.Count == 0 ||
             !candidate.AppliedProgressionReceipts[
                 candidate.AppliedProgressionReceipts.Count - 1].Equals(
                     candidate.LastProgressionReceipt.Value)))
        {
            error = "Applied progression history does not end with the last receipt.";
            return false;
        }

        return true;
    }

    private static bool HasProgressionReceiptData(ProgressionReceiptData data) =>
        data != null &&
        (!string.IsNullOrWhiteSpace(data.raidId) ||
         !string.IsNullOrWhiteSpace(data.profileId) ||
         data.resultSequence != 0 ||
         data.consolidatedExperience != 0 ||
         data.resultingLevel != 0);

    private static bool TryReadProgressionReceipt(
        ProgressionReceiptData data,
        ProfileId expectedProfileId,
        out ProgressionReceipt receipt)
    {
        receipt = default;
        if (data == null ||
            !string.Equals(data.profileId, expectedProfileId.Value, StringComparison.Ordinal))
        {
            return false;
        }

        receipt = new ProgressionReceipt(
            data.raidId,
            expectedProfileId,
            data.resultSequence,
            data.consolidatedExperience,
            data.resultingLevel);
        return receipt.IsValid;
    }

    private static ItemData[] ToItems(IReadOnlyList<StashItem> items)
    {
        var result = new ItemData[items?.Count ?? 0];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new ItemData
            {
                slot = items[i].Slot,
                lootId = items[i].LootId.Value,
                amount = items[i].Amount,
                instance = EquipmentInstanceData.FromInstance(items[i].Instance),
                expeditionId = items[i].ExpeditionId ?? string.Empty,
                origin = items[i].Origin ?? string.Empty
            };
        }
        return result;
    }

    /// <summary>
    /// A malformed or duplicated binding rejects the profile, like the other local-only state.
    /// A binding whose definition left the catalog or stopped being Quick Slot compatible is
    /// content drift, not corruption, so that slot loads unassigned.
    /// </summary>
    private static bool TryReadQuickSlots(
        SaveData data,
        LootDefinitionCatalog catalog,
        out QuickSlotBindings bindings,
        out string error)
    {
        var ids = new[] { data.quickSlot1, data.quickSlot2, data.quickSlot3, data.quickSlot4 };
        if (!QuickSlotBindings.TryCreate(ids, out bindings, out error))
        {
            return false;
        }

        for (int i = 0; i < QuickSlotBindings.SlotCount; i++)
        {
            if (bindings.IsAssigned(i) &&
                (catalog == null ||
                 !catalog.TryGet(bindings.Get(i).Value, out LootDefinition definition) ||
                 !definition.QuickSlotCompatible))
            {
                bindings = bindings.Without(i);
            }
        }

        return true;
    }

    private static bool TryReadPreparedAbilities(
        string slot1Value,
        string slot2Value,
        IReadOnlyList<AbilityId> unlockedAbilities,
        in CharacterAttributeState attributes,
        AbilityDefinitionCatalog abilityCatalog,
        out PreparedAbilityLoadout loadout,
        out string error)
    {
        loadout = default;
        if (!TryReadOptionalAbilityId(slot1Value, out AbilityId slot1, out error) ||
            !TryReadOptionalAbilityId(slot2Value, out AbilityId slot2, out error))
        {
            return false;
        }

        loadout = new PreparedAbilityLoadout(slot1, slot2);
        return PreparedAbilityLoadout.TryValidate(
            loadout,
            unlockedAbilities,
            attributes,
            abilityCatalog,
            out error);
    }

    private static bool TryReadOptionalAbilityId(
        string value,
        out AbilityId abilityId,
        out string error)
    {
        abilityId = default;
        error = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (!AbilityId.TryCreate(value, out abilityId))
        {
            error = "Prepared ability slots contain a malformed ability ID.";
            return false;
        }

        return true;
    }
    private static string[] ToAbilityIds(IReadOnlyList<AbilityId> abilityIds)
    {
        var result = new string[abilityIds?.Count ?? 0];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = abilityIds[index].Value;
        }

        return result;
    }

    private static bool TryReadAbilityIds(
        string[] values,
        List<AbilityId> destination,
        out string error)
    {
        error = null;
        if (values == null)
        {
            return true;
        }

        var seen = new HashSet<AbilityId>();
        foreach (string value in values)
        {
            if (!AbilityId.TryCreate(value, out AbilityId abilityId))
            {
                error = "Unlocked ability repertoire contains an invalid ability ID.";
                return false;
            }

            if (!seen.Add(abilityId))
            {
                error = "Unlocked ability repertoire contains duplicate ability IDs.";
                return false;
            }

            destination.Add(abilityId);
        }

        return true;
    }

    /// <summary>
    /// Reads the eight Equipment assignments. Legacy weapon slots map to each Set's Main Hand.
    /// </summary>
    private static PreparedEquipmentLoadout ReadPreparedEquipment(
        string weaponSetAMainHand,
        string weaponSetBMainHand,
        string helmet,
        string armor,
        string gloves,
        string boots,
        string weaponSetAOffHand,
        string weaponSetBOffHand) => new(
            ReadLootId(weaponSetAMainHand),
            ReadLootId(weaponSetBMainHand),
            ReadLootId(helmet),
            ReadLootId(armor),
            ReadLootId(gloves),
            ReadLootId(boots),
            ReadLootId(weaponSetAOffHand),
            ReadLootId(weaponSetBOffHand));

    private static LootId ReadLootId(string value) =>
        string.IsNullOrWhiteSpace(value) ? default : new LootId(value);

    private static ReceiptData[] ToReceipts(IReadOnlyList<ExtractionReceipt> receipts)
    {
        var result = new ReceiptData[receipts?.Count ?? 0];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new ReceiptData
            {
                raidId = receipts[i].RaidId,
                profileId = receipts[i].ProfileId.Value,
                resultSequence = receipts[i].ResultSequence
            };
        }
        return result;
    }

    private static ShopReceiptData[] ToShopReceipts(IReadOnlyList<ShopTransactionReceipt> receipts)
    {
        var result = new ShopReceiptData[receipts?.Count ?? 0];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new ShopReceiptData
            {
                timestamp = receipts[i].TransactionId.Timestamp,
                transactionId = receipts[i].TransactionId.Value.ToString("N"),
                profileId = receipts[i].ProfileId.Value
            };
        }
        return result;
    }

    private static ProgressionReceiptData ToProgressionReceipt(
        in ProgressionReceipt receipt) => new()
    {
        raidId = receipt.RaidId,
        profileId = receipt.ProfileId.Value,
        resultSequence = receipt.ResultSequence,
        consolidatedExperience = receipt.ConsolidatedExperience,
        resultingLevel = receipt.ResultingLevel
    };

    private static ProgressionReceiptData[] ToProgressionReceipts(
        IReadOnlyList<ProgressionReceipt> receipts)
    {
        var result = new ProgressionReceiptData[receipts?.Count ?? 0];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = ToProgressionReceipt(receipts[index]);
        }

        return result;
    }

    private static MissionInstanceStateData[] ToMissions(IReadOnlyList<MissionInstanceState> missions)
    {
        var result = new MissionInstanceStateData[missions?.Count ?? 0];
        for (int i = 0; i < result.Length; i++)
        {
            var mission = missions[i];
            var progressArray = new ObjectiveProgressStateData[mission.ObjectiveProgress.Count];
            int pIdx = 0;
            foreach (var kvp in mission.ObjectiveProgress)
            {
                progressArray[pIdx++] = new ObjectiveProgressStateData { index = kvp.Key, currentAmount = kvp.Value.CurrentAmount, expeditionId = kvp.Value.ExpeditionId };
            }

            result[i] = new MissionInstanceStateData
            {
                missionId = mission.MissionId.Value,
                state = (int)mission.State,
                currentPhaseIndex = mission.CurrentPhaseIndex,
                objectiveProgress = progressArray
            };
        }
        return result;
    }

    private static bool TryReadItems(ItemData[] data, LootDefinitionCatalog catalog, List<StashItem> destination, string label, out string error, bool physical = false, bool aggregate = false)
    {
        error = null;
        if (data == null)
        {
            return true;
        }

        var seen = new HashSet<LootId>();
        var slots = new HashSet<int>();
        int index = 0;
        foreach (ItemData item in data)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.lootId))
            {
                error = $"{label} contains an invalid loot item.";
                return false;
            }
            var lootId = new LootId(item.lootId);
            if (item.amount <= 0)
            {
                error = $"{label} contains an invalid loot item amount.";
                return false;
            }
            int slot = physical ? item.slot : index;
            if (physical ? slot < 0 || !slots.Add(slot) : !aggregate && !seen.Add(lootId))
            {
                error = physical ? $"{label} contains invalid or duplicate slots." : $"{label} contains duplicate loot IDs.";
                return false;
            }
            EquipmentInstance instance = null;
            if (item.instance != null)
            {
                if (catalog != null && catalog.TryGet(lootId.Value, out var definition))
                {
                    if (item.amount != 1 || !item.instance.TryToInstance(definition, false, out instance, out error, catalog))
                        return false;
                }
                else if (!string.IsNullOrEmpty(item.instance.instanceId))
                {
                    instance = new EquipmentInstance(item.instance.instanceId, null);
                }
            }
            destination.Add(new StashItem(lootId, item.amount, aggregate ? -1 : slot, instance, item.expeditionId ?? string.Empty, item.origin ?? string.Empty));
            index++;
        }
        return true;
    }

    private static bool ValidateInstanceItems(IReadOnlyList<StashItem> items, LootDefinitionCatalog catalog)
    {
        foreach (var item in items)
        {
            if (catalog == null || !catalog.TryGet(item.LootId.Value, out var definition)) continue;
            if (EquipmentInstance.IsEquipment(definition) != (item.Instance != null)) return false;
            if (item.Instance != null && (item.Amount != 1 || !item.Instance.HasIdentity)) return false;
        }
        return true;
    }

    private static PreparedEquipmentLoadout PlainCacheEquipment(PreparedEquipmentLoadout equipment, LootDefinitionCatalog catalog)
    {
        foreach (var slot in EquipmentSlotRules.AllSlots)
            if (equipment.Get(slot).IsValid && catalog.TryGet(equipment.Get(slot).Value, out var definition))
                equipment = equipment.With(slot, equipment.Get(slot), EquipmentInstance.PlainCache(definition));
        return equipment;
    }

    private static void ExpandLegacyEquipment(List<StashItem> items, LootDefinitionCatalog catalog)
    {
        // Reserve every original address before allocating extras, including overflow.
        var extras = new List<StashItem>();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!catalog.TryGet(item.LootId.Value, out var definition) || !EquipmentInstance.IsEquipment(definition)) continue;
            var plain = EquipmentInstance.PlainCache(definition);
            items[i] = new StashItem(item.LootId, 1, item.Slot, plain, item.ExpeditionId, item.Origin);
            for (int unit = 1; unit < item.Amount; unit++) extras.Add(new StashItem(item.LootId, 1, -1, plain, item.ExpeditionId, item.Origin));
        }
        foreach (var item in extras) items.Add(item.AtSlot(ProfileInventoryRules.LowestFreeSlot(items)));
        items.Sort((a, b) => a.Slot.CompareTo(b.Slot));
    }

    private static bool TryReadItems(ItemData[] data, LootDefinitionCatalog catalog, out List<StashItem> items, string label, out string error, bool physical = false, bool aggregate = false)
    {
        items = new List<StashItem>();
        return TryReadItems(data, catalog, items, label, out error, physical, aggregate);
    }
}
