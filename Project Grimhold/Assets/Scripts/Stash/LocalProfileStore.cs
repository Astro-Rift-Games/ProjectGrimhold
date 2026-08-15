using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Atomic application-level operations over the local profile aggregate.
/// </summary>
public sealed class LocalProfileStore
{
    private readonly object _sync = new();
    private readonly ILocalProfileRepository _repository;
    private readonly ProfileId _profileId;

    public event Action<ProfileId> ProfileCommitted;

    public ProfileId ProfileId => _profileId;
    public LocalProfilePersistenceStatus Status => _repository.Status;
    public string LastError => _repository.LastError;
    public bool IsAvailable => Status == LocalProfilePersistenceStatus.Ready || Status == LocalProfilePersistenceStatus.RecoveredFromBackup;

    public LocalProfileStore(ILocalProfileRepository repository, ProfileId profileId)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _profileId = profileId;
    }

    public IReadOnlyList<StashItem> GetStash() =>
        _repository.Snapshot != null ? _repository.Snapshot.Stash : Array.Empty<StashItem>();

    public IReadOnlyList<StashItem> GetLoadout() =>
        _repository.Snapshot != null ? _repository.Snapshot.Loadout : Array.Empty<StashItem>();
    public PendingLoadoutReservation PendingReservation => _repository.Snapshot?.PendingReservation;
    public long GetCurrency() =>
        _repository.Snapshot != null ? _repository.Snapshot.Currency : LocalProfileSnapshot.InitialCurrency;

    public StashOperationResult TryCreditCurrency(long amount)
    {
        if (amount <= 0) return StashOperationResult.InvalidInventory;
        if (_repository.Snapshot.Currency > long.MaxValue - amount) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        next.Currency += amount;
        return Commit(next);
    }

    public StashOperationResult TryDebitCurrency(long amount)
    {
        if (amount <= 0) return StashOperationResult.InvalidInventory;
        if (_repository.Snapshot.Currency < amount) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        next.Currency -= amount;
        return Commit(next);
    }

    public StashOperationResult TrySecureLoot(IReadOnlyList<StashItem> items)
    {
        if (!HasValidItems(items)) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        if (!TryMerge(next.Stash, items)) return StashOperationResult.InvalidInventory;
        return Commit(next);
    }

    public StashOperationResult TryCommitPurchase(ShopTransactionReceipt receipt, LootId lootId, int amount, long declaredPrice, bool addToLoadout = false)
    {
        if (!receipt.IsValid || receipt.ProfileId != _profileId || !lootId.IsValid || amount <= 0 || declaredPrice < 0)
            return StashOperationResult.InvalidInventory;

        var current = _repository.Snapshot;
        if (receipt.TransactionId.Timestamp <= current.ShopIdempotencyWatermark)
            return StashOperationResult.AlreadyApplied;

        foreach (var applied in current.AppliedShopTransactionReceipts)
            if (applied.Equals(receipt)) return StashOperationResult.AlreadyApplied;

        var next = current.Clone();
        
        if (next.Currency < declaredPrice)
            return StashOperationResult.InvalidInventory;
            
        next.Currency -= declaredPrice;

        if (addToLoadout)
        {
            var purchasedItem = new[] { new StashItem(lootId, amount) };
            if (next.Loadout.Count + CountNewSlots(next.Loadout, purchasedItem) > LocalProfileSnapshot.MaxLoadoutSlots)
                return StashOperationResult.PersistenceFailed;

            if (!TryMerge(next.Loadout, purchasedItem))
                return StashOperationResult.PersistenceFailed;
        }

        next.AppliedShopTransactionReceipts.Add(receipt);
        next.AppliedShopTransactionReceipts.Sort((a, b) => a.TransactionId.Timestamp.CompareTo(b.TransactionId.Timestamp));

        while (next.AppliedShopTransactionReceipts.Count > LocalProfileSnapshot.MaxAppliedShopTransactionReceipts)
        {
            var oldest = next.AppliedShopTransactionReceipts[0];
            next.AppliedShopTransactionReceipts.RemoveAt(0);
            if (oldest.TransactionId.Timestamp > next.ShopIdempotencyWatermark)
                next.ShopIdempotencyWatermark = oldest.TransactionId.Timestamp;
        }

        return Commit(next);
    }

    public StashOperationResult TryCommitSale(ShopTransactionReceipt receipt, LootId lootId, int amount, long declaredSellValue, bool isLobby = true)
    {
        if (!receipt.IsValid || receipt.ProfileId != _profileId || !lootId.IsValid || amount <= 0 || declaredSellValue < 0)
            return StashOperationResult.InvalidInventory;

        var current = _repository.Snapshot;
        if (receipt.TransactionId.Timestamp <= current.ShopIdempotencyWatermark)
            return StashOperationResult.AlreadyApplied;

        foreach (var applied in current.AppliedShopTransactionReceipts)
            if (applied.Equals(receipt)) return StashOperationResult.AlreadyApplied;

        var next = current.Clone();

        if (next.Currency > long.MaxValue - declaredSellValue)
            return StashOperationResult.InvalidInventory;

        next.Currency += declaredSellValue;
        
        int availableInLoadout = FindAmount(next.Loadout, lootId);
        if (isLobby)
        {
            if (availableInLoadout < amount)
            {
                return StashOperationResult.InvalidInventory;
            }
            TryRemove(next.Loadout, lootId, amount);
        }
        else
        {
            // In a raid, the item might be freshly looted and thus not in the persistent loadout yet.
            // We only remove it if it was brought from the lobby.
            if (availableInLoadout > 0)
            {
                int amountToRemove = System.Math.Min(availableInLoadout, amount);
                TryRemove(next.Loadout, lootId, amountToRemove);
            }
        }

        next.AppliedShopTransactionReceipts.Add(receipt);
        next.AppliedShopTransactionReceipts.Sort((a, b) => a.TransactionId.Timestamp.CompareTo(b.TransactionId.Timestamp));

        while (next.AppliedShopTransactionReceipts.Count > LocalProfileSnapshot.MaxAppliedShopTransactionReceipts)
        {
            var oldest = next.AppliedShopTransactionReceipts[0];
            next.AppliedShopTransactionReceipts.RemoveAt(0);
            if (oldest.TransactionId.Timestamp > next.ShopIdempotencyWatermark)
                next.ShopIdempotencyWatermark = oldest.TransactionId.Timestamp;
        }

        return Commit(next);
    }

    public StashOperationResult TryConsumeLoot(LootId lootId, int amount)
    {
        if (!lootId.IsValid || amount <= 0) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        if (!TryRemove(next.Stash, lootId, amount)) return StashOperationResult.InvalidInventory;
        return Commit(next);
    }

    public StashOperationResult TryTransferToLoadout(LootId lootId, int amount)
    {
        if (!lootId.IsValid || amount <= 0) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        int available = FindAmount(next.Stash, lootId);
        if (available <= 0) return StashOperationResult.InvalidInventory;
        int actualAmount = Math.Min(amount, available);
        if (FindIndex(next.Loadout, lootId) < 0 && next.Loadout.Count >= LocalProfileSnapshot.MaxLoadoutSlots)
            return StashOperationResult.PersistenceFailed;
        if (!TryRemove(next.Stash, lootId, actualAmount) || !TryMerge(next.Loadout, new[] { new StashItem(lootId, actualAmount) }))
            return StashOperationResult.InvalidInventory;
        return Commit(next);
    }

    public StashOperationResult TryTransferToStash(LootId lootId, int amount)
    {
        if (!lootId.IsValid || amount <= 0) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        int available = FindAmount(next.Loadout, lootId);
        if (available <= 0) return StashOperationResult.InvalidInventory;
        int actualAmount = Math.Min(amount, available);
        if (!TryRemove(next.Loadout, lootId, actualAmount) || !TryMerge(next.Stash, new[] { new StashItem(lootId, actualAmount) }))
            return StashOperationResult.InvalidInventory;
        return Commit(next);
    }

    public StashOperationResult TryTransferAllToLoadout()
    {
        var next = _repository.Snapshot.Clone();
        if (next.Stash.Count == 0) return StashOperationResult.Success;
        int newSlots = 0;
        foreach (StashItem item in next.Stash)
            if (FindIndex(next.Loadout, item.LootId) < 0) newSlots++;
        if (next.Loadout.Count + newSlots > LocalProfileSnapshot.MaxLoadoutSlots)
            return StashOperationResult.PersistenceFailed;
        if (!TryMerge(next.Loadout, next.Stash)) return StashOperationResult.InvalidInventory;
        next.Stash.Clear();
        return Commit(next);
    }

    public StashOperationResult TryTransferAllToStash()
    {
        var next = _repository.Snapshot.Clone();
        if (next.Loadout.Count == 0) return StashOperationResult.Success;
        if (!TryMerge(next.Stash, next.Loadout)) return StashOperationResult.InvalidInventory;
        next.Loadout.Clear();
        return Commit(next);
    }

    public StashOperationResult TryImportItems(IReadOnlyList<StashItem> items)
    {
        if (!HasValidItems(items)) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        int newSlots = 0;
        foreach (StashItem item in items)
            if (FindIndex(next.Loadout, item.LootId) < 0) newSlots++;
        if (next.Loadout.Count + newSlots > LocalProfileSnapshot.MaxLoadoutSlots)
            return StashOperationResult.PersistenceFailed;
        if (!TryMerge(next.Loadout, items)) return StashOperationResult.InvalidInventory;
        return Commit(next);
    }

    public StashOperationResult TryCreateLoadoutReservation(string reservationId, out IReadOnlyList<StashItem> items)
    {
        items = Array.Empty<StashItem>();
        if (string.IsNullOrWhiteSpace(reservationId) || !IsAvailable) return StashOperationResult.InvalidInventory;
        var current = _repository.Snapshot;
        if (current.PendingReservation != null)
        {
            if (!string.Equals(current.PendingReservation.ReservationId, reservationId, StringComparison.Ordinal))
                return StashOperationResult.InvalidInventory;

            items = new List<StashItem>(current.PendingReservation.Items).AsReadOnly();
            return StashOperationResult.Success;
        }
        var next = current.Clone();
        items = new List<StashItem>(next.Loadout).AsReadOnly();
        next.PendingReservation = new PendingLoadoutReservation(reservationId, next.Loadout);
        next.Loadout.Clear();
        StashOperationResult result = Commit(next);
        if (result != StashOperationResult.Success) items = Array.Empty<StashItem>();
        return result;
    }

    public StashOperationResult TryConfirmLoadoutReservation(string reservationId)
    {
        var current = _repository.Snapshot;
        if (current.PendingReservation == null || !string.Equals(current.PendingReservation.ReservationId, reservationId, StringComparison.Ordinal))
            return StashOperationResult.InvalidInventory;
        var next = current.Clone();
        next.PendingReservation = null;
        return Commit(next);
    }

    public StashOperationResult TryRollbackLoadoutReservation(string reservationId)
    {
        var current = _repository.Snapshot;
        if (current.PendingReservation == null || !string.Equals(current.PendingReservation.ReservationId, reservationId, StringComparison.Ordinal))
            return StashOperationResult.InvalidInventory;
        var next = current.Clone();
        if (next.Loadout.Count + CountNewSlots(next.Loadout, next.PendingReservation.Items) > LocalProfileSnapshot.MaxLoadoutSlots ||
            !TryMerge(next.Loadout, next.PendingReservation.Items)) return StashOperationResult.PersistenceFailed;
        next.PendingReservation = null;
        return Commit(next);
    }

    public StashOperationResult TryCommitExtraction(ExtractionReceipt receipt, IReadOnlyList<StashItem> items)
    {
        if (!receipt.IsValid || receipt.ProfileId != _profileId || !HasValidItems(items, allowEmpty: true))
            return StashOperationResult.InvalidInventory;
        foreach (ExtractionReceipt applied in _repository.Snapshot.AppliedExtractionReceipts)
            if (applied.Equals(receipt)) return StashOperationResult.AlreadySecured;
        var next = _repository.Snapshot.Clone();
        
        var loadoutOverflow = new List<StashItem>();
        foreach (var item in items)
        {
            var singleItemArr = new[] { item };
            if (next.Loadout.Count + CountNewSlots(next.Loadout, singleItemArr) <= LocalProfileSnapshot.MaxLoadoutSlots)
            {
                if (!TryMerge(next.Loadout, singleItemArr))
                    loadoutOverflow.Add(item);
            }
            else
            {
                loadoutOverflow.Add(item);
            }
        }

        if (loadoutOverflow.Count > 0)
        {
            if (!TryMerge(next.Stash, loadoutOverflow))
                return StashOperationResult.InvalidInventory;
        }

        next.AppliedExtractionReceipts.Add(receipt);
        while (next.AppliedExtractionReceipts.Count > LocalProfileSnapshot.MaxAppliedExtractionReceipts)
            next.AppliedExtractionReceipts.RemoveAt(0);
        return Commit(next);
    }

    private StashOperationResult Commit(LocalProfileSnapshot next)
    {
        lock (_sync)
        {
            string error = null;
            bool saved = IsAvailable && _repository.TrySave(next, out error);
            if (!saved)
            {
                Debug.LogError(
                    $"[LocalProfileStore] Commit failed. Available={IsAvailable}; " +
                    $"Status={Status}; Error={error ?? _repository.LastError ?? "none"}.");
                return StashOperationResult.PersistenceFailed;
            }
            ProfileCommitted?.Invoke(_profileId);
            return StashOperationResult.Success;
        }
    }

    private static bool HasValidItems(IReadOnlyList<StashItem> items, bool allowEmpty = false)
    {
        if (items == null || (!allowEmpty && items.Count == 0)) return false;
        var seen = new HashSet<LootId>();
        foreach (StashItem item in items)
            if (!item.IsValid || !seen.Add(item.LootId)) return false;
        return true;
    }

    private static bool TryMerge(List<StashItem> destination, IReadOnlyList<StashItem> incoming)
    {
        foreach (StashItem item in incoming)
        {
            int index = FindIndex(destination, item.LootId);
            if (index < 0) destination.Add(item);
            else if (destination[index].Amount > int.MaxValue - item.Amount) return false;
            else destination[index] = new StashItem(item.LootId, destination[index].Amount + item.Amount);
        }
        return true;
    }

    private static bool TryRemove(List<StashItem> items, LootId lootId, int amount)
    {
        int index = FindIndex(items, lootId);
        if (index < 0 || items[index].Amount < amount) return false;
        int remaining = items[index].Amount - amount;
        if (remaining == 0) items.RemoveAt(index);
        else items[index] = new StashItem(lootId, remaining);
        return true;
    }

    private static int FindIndex(IReadOnlyList<StashItem> items, LootId lootId)
    {
        for (int i = 0; i < items.Count; i++) if (items[i].LootId == lootId) return i;
        return -1;
    }

    private static int FindAmount(IReadOnlyList<StashItem> items, LootId lootId)
    {
        int index = FindIndex(items, lootId);
        return index >= 0 ? items[index].Amount : 0;
    }

    private static int CountNewSlots(IReadOnlyList<StashItem> destination, IReadOnlyList<StashItem> incoming)
    {
        int result = 0;
        foreach (StashItem item in incoming) if (FindIndex(destination, item.LootId) < 0) result++;
        return result;
    }
}
