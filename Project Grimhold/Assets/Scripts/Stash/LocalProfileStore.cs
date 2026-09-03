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
    private readonly LootDefinitionCatalog _lootCatalog;
    private readonly LootId _recoveryWeaponLootId;

    public event Action<ProfileId> ProfileCommitted;

    public ProfileId ProfileId => _profileId;
    public LocalProfilePersistenceStatus Status => _repository.Status;
    public string LastError => _repository.LastError;
    public bool IsAvailable => Status == LocalProfilePersistenceStatus.Ready || Status == LocalProfilePersistenceStatus.RecoveredFromBackup;

    public LocalProfileStore(
        ILocalProfileRepository repository,
        ProfileId profileId,
        LootDefinitionCatalog lootCatalog = null,
        LootId recoveryWeaponLootId = default)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _profileId = profileId;
        _lootCatalog = lootCatalog;
        _recoveryWeaponLootId = recoveryWeaponLootId;
    }

    public IReadOnlyList<StashItem> GetStash() =>
        _repository.Snapshot != null ? _repository.Snapshot.Stash : Array.Empty<StashItem>();

    public IReadOnlyList<StashItem> GetLoadout() =>
        _repository.Snapshot != null ? _repository.Snapshot.Loadout : Array.Empty<StashItem>();
    public PreparedEquipmentLoadout GetPreparedEquipment() =>
        _repository.Snapshot != null ? _repository.Snapshot.PreparedEquipment : default;
    public PendingLoadoutReservation PendingReservation => 
        _repository.Snapshot?.PendingReservation;
    public PendingExtractionCommit PendingExtractionCommit => 
        _repository.Snapshot?.PendingExtractionCommit;
    public long GetCurrency() =>
        _repository.Snapshot != null ? _repository.Snapshot.Currency : LocalProfileSnapshot.InitialCurrency;
    public int GetLevel() =>
        _repository.Snapshot != null ? _repository.Snapshot.Level : ExperienceCurve.InitialLevel;
    public long GetCurrentExperience() =>
        _repository.Snapshot != null ? _repository.Snapshot.CurrentExperience : 0L;
    public int GetLastAppliedProgressionResultSequence() =>
        _repository.Snapshot != null ? _repository.Snapshot.LastAppliedProgressionResultSequence : 0;

    public bool TryGetCharacterAttributeState(out CharacterAttributeState state)
    {
        lock (_sync)
        {
            state = default;
            LocalProfileSnapshot snapshot = _repository.Snapshot;
            if (!IsAvailable || snapshot == null || snapshot.ProfileId != _profileId)
            {
                return false;
            }

            state = snapshot.CharacterAttributes;
            return true;
        }
    }

    public CharacterAttributeAssignmentCommitResult TryAssignCharacterAttribute(
        CharacterAttribute attribute,
        out CharacterAttributeAssignmentFailure failure)
    {
        lock (_sync)
        {
            failure = CharacterAttributeAssignmentFailure.None;
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return CharacterAttributeAssignmentCommitResult.Unavailable;
            }

            if (!CharacterAttributeAssignmentRules.TryAssign(
                    ProgressionBalanceDefaults.InitialMaximumAttributeValue,
                    current.CharacterAttributes,
                    attribute,
                    out CharacterAttributeState candidate,
                    out failure))
            {
                return CharacterAttributeAssignmentCommitResult.Rejected;
            }

            LocalProfileSnapshot next = current.Clone();
            next.CharacterAttributes = candidate;
            return Commit(next) == StashOperationResult.Success
                ? CharacterAttributeAssignmentCommitResult.Success
                : CharacterAttributeAssignmentCommitResult.PersistenceFailed;
        }
    }



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

    /// <summary>
    /// Assigns one owned unit to an Equipment slot. The item is explicitly removed from
    /// the Loadout (or Stash) because PreparedEquipment now exclusively owns it.
    /// </summary>
    public StashOperationResult TryAssignPreparedEquipment(EquipmentSlot slot, LootId lootId)
    {
        if (!EquipmentSlotRules.IsEquipmentSlot(slot) || !lootId.IsValid || _lootCatalog == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        LocalProfileSnapshot current = _repository.Snapshot;
        if (!IsAvailable || current == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        LocalProfileSnapshot next = current.Clone();
        
        // Refund previous item in the slot back to Loadout
        LootId previousLootId = next.PreparedEquipment.Get(slot);
        if (previousLootId.IsValid)
        {
            if (FindIndex(next.Loadout, previousLootId) < 0 && next.Loadout.Count >= LocalProfileSnapshot.MaxLoadoutSlots)
            {
                return StashOperationResult.PersistenceFailed; // Loadout full, can't unequip
            }
            if (!TryMerge(next.Loadout, new[] { new StashItem(previousLootId, 1) }))
            {
                return StashOperationResult.PersistenceFailed;
            }
        }

        PreparedEquipmentLoadout candidate = next.PreparedEquipment.With(slot, lootId);

        if (EquipmentSlotRules.IsWeaponSlot(slot) &&
            !PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                candidate, current.CharacterAttributes, _lootCatalog, out _))
        {
            return StashOperationResult.AttributeRequirementsNotMet;
        }

        // Deduct the new item from Loadout or Stash
        if (FindAmount(next.Loadout, lootId) >= 1)
        {
            TryRemove(next.Loadout, lootId, 1);
        }
        else if (FindAmount(next.Stash, lootId) >= 1)
        {
            TryRemove(next.Stash, lootId, 1);
        }
        else
        {
            return StashOperationResult.InvalidInventory;
        }

        if (!PreparedEquipmentLoadout.TryValidate(
                candidate,
                _lootCatalog,
                requireWeapon: false,
                out _))
        {
            return StashOperationResult.InvalidInventory;
        }

        next.PreparedEquipment = candidate;
        return Commit(next);
    }

    /// <summary>Releases one Equipment slot and returns the unit to the Loadout.</summary>
    public StashOperationResult TryClearPreparedEquipment(EquipmentSlot slot)
    {
        if (!EquipmentSlotRules.IsEquipmentSlot(slot))
        {
            return StashOperationResult.InvalidInventory;
        }

        LocalProfileSnapshot current = _repository.Snapshot;
        if (!IsAvailable || current == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        LocalProfileSnapshot next = current.Clone();
        LootId previousLootId = next.PreparedEquipment.Get(slot);

        if (previousLootId.IsValid)
        {
            if (FindIndex(next.Loadout, previousLootId) < 0 && next.Loadout.Count >= LocalProfileSnapshot.MaxLoadoutSlots)
            {
                return StashOperationResult.PersistenceFailed; // Loadout full, can't unequip
            }
            if (!TryMerge(next.Loadout, new[] { new StashItem(previousLootId, 1) }))
            {
                return StashOperationResult.PersistenceFailed;
            }
        }

        next.PreparedEquipment = next.PreparedEquipment.Without(slot);
        return Commit(next);
    }

    /// <summary>
    /// Normalizes the local Loadout and prepared Weapon Equipment so the aggregate holds exactly
    /// one valid effective weapon in Weapon Slot 1 before a raid reservation is attempted.
    /// The operation is atomic, deterministic and idempotent: a profile that is already prepared
    /// commits nothing, so retrying a launch never grants or duplicates a recovery weapon.
    /// </summary>
    public ExpeditionPreparationResult TryPrepareExpeditionEquipment()
    {
        LocalProfileSnapshot current = _repository.Snapshot;
        if (!IsAvailable || current == null || _lootCatalog == null)
        {
            return ExpeditionPreparationResult.ProfileUnavailable;
        }

        // A reservation already owns the prepared equipment; re-preparing would double-grant.
        if (current.PendingReservation != null)
        {
            return ExpeditionPreparationResult.Success;
        }

        PreparedEquipmentLoadout prepared = current.PreparedEquipment;

        // A persisted assignment that no longer resolves to an owned, usable weapon is corruption.
        // It fails explicitly instead of being overwritten or hidden behind a recovery grant.
        if (prepared.HasAnyWeapon && !PreparedEquipmentLoadout.TryValidate(
                prepared,
                _lootCatalog,
                requireWeapon: false,
                out _))
        {
            return ExpeditionPreparationResult.InvalidPreparedWeapon;
        }

        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                prepared,
                current.CharacterAttributes,
                _lootCatalog,
                out _))
        {
            return ExpeditionPreparationResult.AttributeRequirementsNotMet;
        }

        // The effective weapon is already prepared; ownership stays untouched.
        if (prepared.HasWeaponSlot1)
        {
            return ExpeditionPreparationResult.Success;
        }

        // Only the optional slot is occupied: normalize the effective selection towards it.
        // Both assignments are non-owning references, so no unit moves.
        if (prepared.HasWeaponSlot2)
        {
            LocalProfileSnapshot normalized = current.Clone();
            normalized.PreparedEquipment = prepared
                .Without(EquipmentSlot.WeaponSlot2)
                .With(EquipmentSlot.WeaponSlot1, prepared.WeaponSlot2);
            return Commit(normalized) == StashOperationResult.Success
                ? ExpeditionPreparationResult.Success
                : ExpeditionPreparationResult.PersistenceFailed;
        }

        return TryPrepareRecoveryWeapon(current);
    }

    /// <summary>
    /// Guarantees Town access to exactly one recovery weapon when no weapon is prepared. A unit
    /// already owned is reused before a new one is granted, so the grant cannot accumulate.
    /// </summary>
    private ExpeditionPreparationResult TryPrepareRecoveryWeapon(LocalProfileSnapshot current)
    {
        if (!PreparedEquipmentLoadout.IsUsableWeaponDefinition(_recoveryWeaponLootId, _lootCatalog))
        {
            return ExpeditionPreparationResult.RecoveryWeaponUnavailable;
        }

        var recovery = new PreparedEquipmentLoadout(_recoveryWeaponLootId, default);
        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                recovery,
                current.CharacterAttributes,
                _lootCatalog,
                out _))
        {
            return ExpeditionPreparationResult.AttributeRequirementsNotMet;
        }

        LocalProfileSnapshot next = current.Clone();
        
        // Remove from Stash or Loadout if owned, otherwise just grant it directly
        if (FindAmount(next.Loadout, _recoveryWeaponLootId) >= 1)
        {
            TryRemove(next.Loadout, _recoveryWeaponLootId, 1);
        }
        else if (FindAmount(next.Stash, _recoveryWeaponLootId) >= 1)
        {
            TryRemove(next.Stash, _recoveryWeaponLootId, 1);
        }

        // Only the weapon slot is granted: prepared armor is untouched by the recovery guarantee.
        next.PreparedEquipment = next.PreparedEquipment
            .With(EquipmentSlot.WeaponSlot1, _recoveryWeaponLootId);
        return Commit(next) == StashOperationResult.Success
            ? ExpeditionPreparationResult.Success
            : ExpeditionPreparationResult.PersistenceFailed;
    }

    public StashOperationResult TryCreateLoadoutReservation(
        string reservationId,
        out PendingLoadoutReservation reservation)
    {
        reservation = null;
        if (string.IsNullOrWhiteSpace(reservationId) || !IsAvailable) return StashOperationResult.InvalidInventory;
        var current = _repository.Snapshot;
        if (current.PendingReservation != null)
        {
            if (!string.Equals(current.PendingReservation.ReservationId, reservationId, StringComparison.Ordinal))
                return StashOperationResult.InvalidInventory;

            if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                    current.PendingReservation.PreparedEquipment,
                    current.CharacterAttributes,
                    _lootCatalog,
                    out _))
            {
                return StashOperationResult.AttributeRequirementsNotMet;
            }

            reservation = current.PendingReservation.Clone();
            return StashOperationResult.Success;
        }

        if (!PreparedEquipmentLoadout.TryValidate(
                current.PreparedEquipment,
                _lootCatalog,
                requireWeapon: true,
                out _))
        {
            return StashOperationResult.InvalidInventory;
        }

        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                current.PreparedEquipment,
                current.CharacterAttributes,
                _lootCatalog,
                out _))
        {
            return StashOperationResult.AttributeRequirementsNotMet;
        }

        var next = current.Clone();
        next.PendingReservation = new PendingLoadoutReservation(
            reservationId,
            next.Loadout,
            next.PreparedEquipment);
        next.Loadout.Clear();
        next.PreparedEquipment = default;
        StashOperationResult result = Commit(next);
        if (result == StashOperationResult.Success)
        {
            reservation = _repository.Snapshot.PendingReservation.Clone();
        }
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
        PreparedEquipmentLoadout restoredWeapons = next.PendingReservation.PreparedEquipment;
        if (!PreparedEquipmentLoadout.TryValidate(
                restoredWeapons,
                _lootCatalog,
                requireWeapon: true,
                out _))
        {
            return StashOperationResult.PersistenceFailed;
        }
        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                restoredWeapons,
                current.CharacterAttributes,
                _lootCatalog,
                out _))
        {
            return StashOperationResult.AttributeRequirementsNotMet;
        }
        next.PreparedEquipment = restoredWeapons;
        next.PendingReservation = null;
        return Commit(next);
    }

    /// <summary>
    /// Commits the exact authoritative raid inventory into the persistent Loadout.
    ///
    /// A valid raid admission consumes the pending reservation and leaves the
    /// persistent Loadout empty. A new receipt therefore fails atomically when
    /// that invariant is not true; extracted items are never redirected to Stash.
    /// </summary>
    public StashOperationResult TryCommitExtraction(
        ExtractionReceipt receipt, 
        IReadOnlyList<StashItem> items,
        long consolidatedExperience,
        int resultingLevel)
    {
        if (!receipt.IsValid || receipt.ProfileId != _profileId || !HasValidItems(items, allowEmpty: true))
            return StashOperationResult.InvalidInventory;

        if (items.Count > LocalProfileSnapshot.MaxLoadoutSlots)
            return StashOperationResult.PersistenceFailed;

        LocalProfileSnapshot current = _repository.Snapshot;
        foreach (ExtractionReceipt applied in current.AppliedExtractionReceipts)
            if (applied.Equals(receipt)) return StashOperationResult.AlreadySecured;

        if (current.Loadout.Count != 0)
            return StashOperationResult.PersistenceFailed;

        LocalProfileSnapshot next = current.Clone();
        next.Loadout.AddRange(items);
        next.AppliedExtractionReceipts.Add(receipt);
        while (next.AppliedExtractionReceipts.Count > LocalProfileSnapshot.MaxAppliedExtractionReceipts)
            next.AppliedExtractionReceipts.RemoveAt(0);

        next.PendingExtractionCommit = new PendingExtractionCommit(
            receipt,
            items,
            consolidatedExperience,
            resultingLevel);

        return Commit(next);
    }
    public void ClearPendingExtractionCommit()
    {
        LocalProfileSnapshot current = _repository.Snapshot;
        if (current == null || current.PendingExtractionCommit == null)
            return;
            
        LocalProfileSnapshot next = current.Clone();
        next.PendingExtractionCommit = null;
        Commit(next);
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
