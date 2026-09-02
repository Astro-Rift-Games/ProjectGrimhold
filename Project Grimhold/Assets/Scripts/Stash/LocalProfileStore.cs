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
    public PendingLoadoutReservation PendingReservation => _repository.Snapshot?.PendingReservation;
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

    /// <summary>
    /// Applies one resolved raid reward and its idempotency watermark as one process-local mutation.
    /// The observable repository snapshot is unchanged unless the complete candidate saves.
    /// </summary>
    public ProgressionCommitResult TryCommitProgression(
        in ProgressionReceipt receipt,
        in ExpeditionExperienceResolution resolution)
    {
        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || !receipt.IsValid ||
                receipt.ProfileId != _profileId || !resolution.IsResolved ||
                receipt.ConsolidatedExperience != resolution.ConsolidatedExperience)
            {
                return ProgressionCommitResult.Invalid;
            }

            int watermark = current.LastAppliedProgressionResultSequence;
            if ((watermark == 0 && current.LastProgressionReceipt.HasValue) ||
                (watermark > 0 &&
                 (!current.LastProgressionReceipt.HasValue ||
                  current.LastProgressionReceipt.Value.ResultSequence != watermark ||
                  current.LastProgressionReceipt.Value.ProfileId != current.ProfileId)))
            {
                return ProgressionCommitResult.Invalid;
            }

            if (receipt.ResultSequence < watermark)
            {
                return ProgressionCommitResult.Stale;
            }

            if (receipt.ResultSequence == watermark)
            {
                return current.LastProgressionReceipt.HasValue &&
                       current.LastProgressionReceipt.Value.Equals(receipt)
                    ? ProgressionCommitResult.AlreadyApplied
                    : ProgressionCommitResult.Conflict;
            }

            if (watermark == int.MaxValue || receipt.ResultSequence != watermark + 1)
            {
                return ProgressionCommitResult.Invalid;
            }

            if (!ConsolidatedExperienceApplicationRules.TryApply(
                    ProgressionBalanceDefaults.InitialExperienceCurve,
                    default,
                    current.Level,
                    current.CurrentExperience,
                    resolution,
                    out ConsolidatedExperienceApplication application,
                    out _))
            {
                return ProgressionCommitResult.Invalid;
            }

            if (application.Result.ResultingLevel != receipt.ResultingLevel)
            {
                return ProgressionCommitResult.Invalid;
            }

            if (!CharacterAttributePointGrantRules.TryApply(
                    ProgressionBalanceDefaults.InitialAttributePointsPerLevel,
                    default,
                    current.CharacterAttributes,
                    application.Result,
                    out CharacterAttributePointGrant pointGrant,
                    out _))
            {
                return ProgressionCommitResult.Invalid;
            }

            LocalProfileSnapshot next = current.Clone();
            next.Level = application.Result.ResultingLevel;
            next.CurrentExperience = application.Result.ResultingExperience;
            next.CharacterAttributes = pointGrant.Result;
            next.LastAppliedProgressionResultSequence = receipt.ResultSequence;
            next.LastProgressionReceipt = receipt;
            next.AppliedProgressionReceipts.Add(receipt);
            while (next.AppliedProgressionReceipts.Count >
                   LocalProfileSnapshot.MaxAppliedProgressionReceipts)
            {
                next.AppliedProgressionReceipts.RemoveAt(0);
            }

            return Commit(next) == StashOperationResult.Success
                ? ProgressionCommitResult.Success
                : ProgressionCommitResult.PersistenceFailed;
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
            ReconcilePreparedEquipment(next);
        }
        else
        {
            // In a raid, the item might be freshly looted and thus not in the persistent loadout yet.
            // We only remove it if it was brought from the lobby.
            if (availableInLoadout > 0)
            {
                int amountToRemove = System.Math.Min(availableInLoadout, amount);
                TryRemove(next.Loadout, lootId, amountToRemove);
                ReconcilePreparedEquipment(next);
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
        ReconcilePreparedEquipment(next);
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
        next.PreparedEquipment = default;
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
    /// Assigns one owned unit to an Equipment slot. Prepared units must belong to the Loadout,
    /// because the Loadout is what the raid reservation transfers. A unit that still lives in the
    /// Stash is therefore moved into the Loadout inside the same atomic commit, so equipping from
    /// either panel of the Stash screen produces the same aggregate.
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
        PreparedEquipmentLoadout candidate = next.PreparedEquipment.With(slot, lootId);

        if (EquipmentSlotRules.IsWeaponSlot(slot) &&
            !PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                candidate, current.CharacterAttributes, _lootCatalog, out _))
        {
            return StashOperationResult.AttributeRequirementsNotMet;
        }

        int required = PreparedEquipmentLoadout.CountReferences(candidate, lootId);
        int missing = required - FindAmount(next.Loadout, lootId);
        if (missing > 0 && !TryPullFromStash(next, lootId, missing))
        {
            return StashOperationResult.InvalidInventory;
        }

        if (!PreparedEquipmentLoadout.TryValidate(
                candidate,
                next.Loadout,
                _lootCatalog,
                requireWeapon: false,
                out _))
        {
            return StashOperationResult.InvalidInventory;
        }

        next.PreparedEquipment = candidate;
        return Commit(next);
    }

    /// <summary>Releases one Equipment slot. The unit stays in the Loadout.</summary>
    public StashOperationResult TryClearPreparedEquipment(EquipmentSlot slot)
    {
        if (!EquipmentSlotRules.IsEquipmentSlot(slot))
        {
            return StashOperationResult.InvalidInventory;
        }

        LocalProfileSnapshot next = _repository.Snapshot.Clone();
        next.PreparedEquipment = next.PreparedEquipment.Without(slot);
        return Commit(next);
    }

    /// <summary>
    /// Moves the missing units of one identity from the Stash into the Loadout. It mutates the
    /// received candidate snapshot only, so the caller still decides whether to commit.
    /// </summary>
    private static bool TryPullFromStash(LocalProfileSnapshot snapshot, LootId lootId, int amount)
    {
        if (amount <= 0 || FindAmount(snapshot.Stash, lootId) < amount)
        {
            return false;
        }

        if (FindIndex(snapshot.Loadout, lootId) < 0 &&
            snapshot.Loadout.Count >= LocalProfileSnapshot.MaxLoadoutSlots)
        {
            return false;
        }

        return TryRemove(snapshot.Stash, lootId, amount) &&
            TryMerge(snapshot.Loadout, new[] { new StashItem(lootId, amount) });
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
                current.Loadout,
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
        if (FindAmount(next.Loadout, _recoveryWeaponLootId) <= 0)
        {
            if (next.Loadout.Count >= LocalProfileSnapshot.MaxLoadoutSlots)
            {
                return ExpeditionPreparationResult.LoadoutFull;
            }

            // Prefer a unit the profile already owns before minting the guaranteed one.
            TryRemove(next.Stash, _recoveryWeaponLootId, 1);
            if (!TryMerge(next.Loadout, new[] { new StashItem(_recoveryWeaponLootId, 1) }))
            {
                return ExpeditionPreparationResult.LoadoutFull;
            }
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
                current.Loadout,
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
                next.Loadout,
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
    public StashOperationResult TryCommitExtraction(ExtractionReceipt receipt, IReadOnlyList<StashItem> items)
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

    /// <summary>
    /// Drops the Equipment assignments whose units no longer belong to the Loadout. Slots are
    /// released from the last one backwards, so a shortage always keeps the earliest assignment.
    /// </summary>
    private void ReconcilePreparedEquipment(LocalProfileSnapshot snapshot)
    {
        PreparedEquipmentLoadout current = snapshot.PreparedEquipment;
        if (!current.HasAnyEquipment)
        {
            return;
        }

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        PreparedEquipmentLoadout candidate = current;
        for (int index = slots.Length - 1; index >= 0; index--)
        {
            EquipmentSlot slot = slots[index];
            LootId lootId = candidate.Get(slot);
            if (!lootId.IsValid)
            {
                continue;
            }

            if (FindAmount(snapshot.Loadout, lootId) <
                PreparedEquipmentLoadout.CountReferences(candidate, lootId))
            {
                candidate = candidate.Without(slot);
            }
        }

        if (!PreparedEquipmentLoadout.TryValidate(
                candidate,
                snapshot.Loadout,
                _lootCatalog,
                requireWeapon: false,
                out _))
        {
            candidate = default;
        }

        snapshot.PreparedEquipment = candidate;
    }
}
