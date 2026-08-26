using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Encodes and validates the versioned local profile JSON DTOs.
/// </summary>
public static class LocalProfileSaveCodec
{
    [Serializable]
    private sealed class SaveData
    {
        public int schemaVersion;
        public string profileId;
        public long currency;
        public int level;
        public long currentExperience;
        public int lastAppliedProgressionResultSequence;
        public ProgressionReceiptData lastProgressionReceipt;
        public ProgressionReceiptData[] appliedProgressionReceipts;
        public ItemData[] stash;
        public ItemData[] loadout;
        public string preparedWeaponSlot1;
        public string preparedWeaponSlot2;
        public string preparedHelmet;
        public string preparedArmor;
        public string preparedGloves;
        public string preparedBoots;
        public ReservationData pendingReservation;
        public ReceiptData[] appliedExtractionReceipts;
        public long shopIdempotencyWatermark;
        public ShopReceiptData[] appliedShopTransactionReceipts;
    }

    [Serializable]
    private sealed class ItemData
    {
        public string lootId;
        public int amount;
    }

    [Serializable]
    private sealed class ReservationData
    {
        public string reservationId;
        public ItemData[] items;
        public string preparedWeaponSlot1;
        public string preparedWeaponSlot2;
        public string preparedHelmet;
        public string preparedArmor;
        public string preparedGloves;
        public string preparedBoots;
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

    public static string Encode(LocalProfileSnapshot snapshot)
    {
        var data = new SaveData
        {
            schemaVersion = snapshot.SchemaVersion,
            profileId = snapshot.ProfileId.Value,
            currency = snapshot.Currency,
            level = snapshot.Level,
            currentExperience = snapshot.CurrentExperience,
            lastAppliedProgressionResultSequence = snapshot.LastAppliedProgressionResultSequence,
            lastProgressionReceipt = snapshot.LastProgressionReceipt.HasValue
                ? ToProgressionReceipt(snapshot.LastProgressionReceipt.Value)
                : null,
            appliedProgressionReceipts = ToProgressionReceipts(snapshot.AppliedProgressionReceipts),
            stash = ToItems(snapshot.Stash),
            loadout = ToItems(snapshot.Loadout),
            preparedWeaponSlot1 = snapshot.PreparedEquipment.WeaponSlot1.Value,
            preparedWeaponSlot2 = snapshot.PreparedEquipment.WeaponSlot2.Value,
            preparedHelmet = snapshot.PreparedEquipment.Helmet.Value,
            preparedArmor = snapshot.PreparedEquipment.Armor.Value,
            preparedGloves = snapshot.PreparedEquipment.Gloves.Value,
            preparedBoots = snapshot.PreparedEquipment.Boots.Value,
            pendingReservation = snapshot.PendingReservation == null ? null : new ReservationData
            {
                reservationId = snapshot.PendingReservation.ReservationId,
                items = ToItems(snapshot.PendingReservation.Items),
                preparedWeaponSlot1 = snapshot.PendingReservation.PreparedEquipment.WeaponSlot1.Value,
                preparedWeaponSlot2 = snapshot.PendingReservation.PreparedEquipment.WeaponSlot2.Value,
                preparedHelmet = snapshot.PendingReservation.PreparedEquipment.Helmet.Value,
                preparedArmor = snapshot.PendingReservation.PreparedEquipment.Armor.Value,
                preparedGloves = snapshot.PendingReservation.PreparedEquipment.Gloves.Value,
                preparedBoots = snapshot.PendingReservation.PreparedEquipment.Boots.Value
            },
            appliedExtractionReceipts = ToReceipts(snapshot.AppliedExtractionReceipts),
            shopIdempotencyWatermark = snapshot.ShopIdempotencyWatermark,
            appliedShopTransactionReceipts = ToShopReceipts(snapshot.AppliedShopTransactionReceipts)
        };
        return JsonUtility.ToJson(data, true);
    }

    public static bool TryDecode(
        string json,
        ProfileId expectedProfileId,
        LootDefinitionCatalog catalog,
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
            data = JsonUtility.FromJson<SaveData>(json);
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

        if (data.schemaVersion > LocalProfileSnapshot.CurrentSchemaVersion)
        {
            status = LocalProfilePersistenceStatus.UnsupportedVersion;
            error = $"Unsupported profile schema version {data.schemaVersion}.";
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
            ProfileId = expectedProfileId
        };
        if (!TryReadItems(data.stash, catalog, candidate.Stash, "stash", out error) ||
            !TryReadItems(data.loadout, catalog, candidate.Loadout, "loadout", out error))
        {
            return false;
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

        if (candidate.Loadout.Count > LocalProfileSnapshot.MaxLoadoutSlots)
        {
            error = "Loadout exceeds the maximum number of slots.";
            return false;
        }

        candidate.PreparedEquipment = ReadPreparedEquipment(
            data.preparedWeaponSlot1,
            data.preparedWeaponSlot2,
            data.preparedHelmet,
            data.preparedArmor,
            data.preparedGloves,
            data.preparedBoots);
        if (!PreparedEquipmentLoadout.TryValidate(
                candidate.PreparedEquipment,
                candidate.Loadout,
                catalog,
                requireWeapon: false,
                out error))
        {
            return false;
        }

        // JsonUtility materializes an empty nested DTO for a serialized null
        // reference. Treat that exact empty shape as "no reservation"; any
        // reservation carrying data still requires a valid identity.
        bool hasPendingReservationData = data.pendingReservation != null &&
            (!string.IsNullOrWhiteSpace(data.pendingReservation.reservationId) ||
             (data.pendingReservation.items != null && data.pendingReservation.items.Length > 0) ||
             !string.IsNullOrWhiteSpace(data.pendingReservation.preparedWeaponSlot1) ||
             !string.IsNullOrWhiteSpace(data.pendingReservation.preparedWeaponSlot2) ||
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

            if (!TryReadItems(data.pendingReservation.items, catalog, out List<StashItem> reservationItems, "reservation", out error))
            {
                return false;
            }
            PreparedEquipmentLoadout reservedEquipment = ReadPreparedEquipment(
                data.pendingReservation.preparedWeaponSlot1,
                data.pendingReservation.preparedWeaponSlot2,
                data.pendingReservation.preparedHelmet,
                data.pendingReservation.preparedArmor,
                data.pendingReservation.preparedGloves,
                data.pendingReservation.preparedBoots);
            if (!PreparedEquipmentLoadout.TryValidate(
                    reservedEquipment,
                    reservationItems,
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
                if (Guid.TryParse(receiptData.transactionId, out Guid parsedGuid))
                {
                    var txId = new ShopTransactionId(receiptData.timestamp, parsedGuid);
                    var receipt = new ShopTransactionReceipt(txId, expectedProfileId);
                    
                    if (receipt.IsValid && string.Equals(receiptData.profileId, expectedProfileId.Value, StringComparison.Ordinal) && seen.Add(receipt))
                    {
                        candidate.AppliedShopTransactionReceipts.Add(receipt);
                    }
                }
            }
        }

        if (candidate.AppliedShopTransactionReceipts.Count > LocalProfileSnapshot.MaxAppliedShopTransactionReceipts)
        {
            error = "Applied shop transaction receipt history exceeds its limit.";
            return false;
        }

        snapshot = candidate;
        status = LocalProfilePersistenceStatus.Ready;
        return true;
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
            result[i] = new ItemData { lootId = items[i].LootId.Value, amount = items[i].Amount };
        }
        return result;
    }

    /// <summary>
    /// Reads the six Equipment assignments. The four armor fields are additive: a profile saved
    /// before they existed decodes them as empty and keeps its weapons.
    /// </summary>
    private static PreparedEquipmentLoadout ReadPreparedEquipment(
        string weaponSlot1,
        string weaponSlot2,
        string helmet,
        string armor,
        string gloves,
        string boots) => new(
            ReadLootId(weaponSlot1),
            ReadLootId(weaponSlot2),
            ReadLootId(helmet),
            ReadLootId(armor),
            ReadLootId(gloves),
            ReadLootId(boots));

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

    private static bool TryReadItems(ItemData[] data, LootDefinitionCatalog catalog, List<StashItem> destination, string label, out string error)
    {
        error = null;
        if (data == null)
        {
            return true;
        }

        var seen = new HashSet<LootId>();
        foreach (ItemData item in data)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.lootId))
            {
                error = $"{label} contains an invalid loot item.";
                return false;
            }
            var lootId = new LootId(item.lootId);
            if (item.amount <= 0 || catalog == null || !catalog.TryGet(lootId.Value, out _))
            {
                error = $"{label} contains an invalid or unknown loot item.";
                return false;
            }
            if (!seen.Add(lootId))
            {
                error = $"{label} contains duplicate loot IDs.";
                return false;
            }
            destination.Add(new StashItem(lootId, item.amount));
        }
        return true;
    }

    private static bool TryReadItems(ItemData[] data, LootDefinitionCatalog catalog, out List<StashItem> items, string label, out string error)
    {
        items = new List<StashItem>();
        return TryReadItems(data, catalog, items, label, out error);
    }
}
