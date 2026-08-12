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
        public ItemData[] stash;
        public ItemData[] loadout;
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

    public static string Encode(LocalProfileSnapshot snapshot)
    {
        var data = new SaveData
        {
            schemaVersion = snapshot.SchemaVersion,
            profileId = snapshot.ProfileId.Value,
            currency = snapshot.Currency,
            stash = ToItems(snapshot.Stash),
            loadout = ToItems(snapshot.Loadout),
            pendingReservation = snapshot.PendingReservation == null ? null : new ReservationData
            {
                reservationId = snapshot.PendingReservation.ReservationId,
                items = ToItems(snapshot.PendingReservation.Items)
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

        if (data.schemaVersion != LocalProfileSnapshot.CurrentSchemaVersion)
        {
            status = LocalProfilePersistenceStatus.UnsupportedVersion;
            error = $"Unsupported profile schema version {data.schemaVersion}.";
            return false;
        }

        if (!expectedProfileId.IsValid || !string.Equals(data.profileId, expectedProfileId.Value, StringComparison.Ordinal))
        {
            error = "Profile ID does not match the local identity.";
            return false;
        }

        var candidate = new LocalProfileSnapshot { ProfileId = expectedProfileId };
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

        if (candidate.Loadout.Count > LocalProfileSnapshot.MaxLoadoutSlots)
        {
            error = "Loadout exceeds the maximum number of slots.";
            return false;
        }

        // JsonUtility materializes an empty nested DTO for a serialized null
        // reference. Treat that exact empty shape as "no reservation"; any
        // reservation carrying data still requires a valid identity.
        bool hasPendingReservationData = data.pendingReservation != null &&
            (!string.IsNullOrWhiteSpace(data.pendingReservation.reservationId) ||
             (data.pendingReservation.items != null && data.pendingReservation.items.Length > 0));
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
            candidate.PendingReservation = new PendingLoadoutReservation(data.pendingReservation.reservationId, reservationItems);
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

    private static ItemData[] ToItems(IReadOnlyList<StashItem> items)
    {
        var result = new ItemData[items?.Count ?? 0];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new ItemData { lootId = items[i].LootId.Value, amount = items[i].Amount };
        }
        return result;
    }

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
