using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Compatibility adapter exposing process-local loadout operations through the
/// existing service contract. It owns no loadout state.
/// </summary>
public sealed class InMemoryPlayerLoadoutService : MonoBehaviour, IPlayerLoadoutService, ISlotPlayerLoadoutService
{
    public int InventorySchemaVersion => _store?.InventorySchemaVersion ?? 1;
    public StashOperationResult EvaluateSplit(ProfileId profile, InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId lootId) =>
        IsProfile(profile) ? _store.EvaluateSplit(list, sourceSlot, amount, destinationSlot, lootId) : StashOperationResult.InvalidInventory;
    public StashOperationResult TrySplit(ProfileId profile, InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId lootId) =>
        IsProfile(profile) ? _store.TrySplit(list, sourceSlot, amount, destinationSlot, lootId) : StashOperationResult.InvalidInventory;
    public StashOperationResult EvaluateTransfer(ProfileId profile, int slot, LootId id, int amount, bool fromStash) =>
        IsProfile(profile) ? _store.EvaluateTransfer(slot, id, amount, fromStash) : StashOperationResult.InvalidInventory;
    public StashOperationResult TryTransfer(ProfileId profile, int slot, LootId id, int amount, bool fromStash) =>
        IsProfile(profile) ? _store.TryTransfer(slot, id, amount, fromStash) : StashOperationResult.InvalidInventory;
    public StashOperationResult EvaluateOrderSlots(ProfileId profile, InventoryList list, IReadOnlyList<int> slots) =>
        IsProfile(profile) ? _store.EvaluateOrderSlots(list, slots) : StashOperationResult.InvalidInventory;
    public StashOperationResult TryOrderSlots(ProfileId profile, InventoryList list, IReadOnlyList<int> slots) =>
        IsProfile(profile) ? _store.TryOrderSlots(list, slots) : StashOperationResult.InvalidInventory;
    public StashOperationResult EvaluateMove(ProfileId profile, InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id) =>
        IsProfile(profile) ? _store.EvaluateMove(from, fromSlot, to, toSlot, id) : StashOperationResult.InvalidInventory;
    public StashOperationResult TryMove(ProfileId profile, InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id) =>
        IsProfile(profile) ? _store.TryMove(from, fromSlot, to, toSlot, id) : StashOperationResult.InvalidInventory;

    public StashOperationResult TryApplyConfirmedTransferAll(ProfileId profile, Grimhold.Backend.TransferAllResult result) =>
        IsProfile(profile) ? _store.ApplyConfirmedTransferAll(result) : StashOperationResult.InvalidInventory;

    public StashOperationResult TryOrder(ProfileId profileId, InventoryList list, IReadOnlyList<LootId> lootIds) =>
        IsProfile(profileId) ? _store.TryOrder(list, lootIds) : StashOperationResult.InvalidInventory;

    public StashOperationResult EvaluateOrder(ProfileId profileId, InventoryList list, IReadOnlyList<LootId> lootIds) =>
        IsProfile(profileId) ? _store.EvaluateOrder(list, lootIds) : StashOperationResult.InvalidInventory;

    private LocalProfileStore _store;

    public event Action<ProfileId> LoadoutChanged;

    public void Initialize(LocalProfileStore store)
    {
        if (_store != null) _store.ProfileCommitted -= OnProfileCommitted;
        _store = store;
        if (_store != null) _store.ProfileCommitted += OnProfileCommitted;
    }

    public IReadOnlyList<StashItem> GetLoadout(ProfileId profileId) => IsProfile(profileId) ? _store.GetLoadout() : Array.Empty<StashItem>();

    public PreparedEquipmentLoadout GetPreparedEquipment(ProfileId profileId) =>
        IsProfile(profileId) ? _store.GetPreparedEquipment() : default;

    public StashOperationResult TryAssignPreparedEquipment(
        ProfileId profileId,
        EquipmentSlot slot,
        LootId lootId) => IsProfile(profileId)
            ? _store.TryAssignPreparedEquipment(slot, lootId)
            : StashOperationResult.InvalidInventory;

    public StashOperationResult TryClearPreparedEquipment(ProfileId profileId, EquipmentSlot slot) =>
        IsProfile(profileId)
            ? _store.TryClearPreparedEquipment(slot)
            : StashOperationResult.InvalidInventory;

    public StashOperationResult TryEquipFromStash(ProfileId profileId, LootId lootId, EquipmentSlot slot) =>
        IsProfile(profileId)
            ? _store.TryEquipFromStash(slot, lootId)
            : StashOperationResult.InvalidInventory;

    public StashOperationResult EvaluateEquipFromStash(
        ProfileId profileId, LootId lootId, EquipmentSlot slot, out PreparedEquipmentLoadout equipment)
    {
        equipment = default;
        return IsProfile(profileId)
            ? _store.EvaluateEquipFromStash(slot, lootId, out equipment)
            : StashOperationResult.InvalidInventory;
    }

    public ExpeditionPreparationResult TryPrepareExpeditionLoadout(ProfileId profileId) =>
        IsProfile(profileId)
            ? _store.TryPrepareExpeditionEquipment()
            : ExpeditionPreparationResult.ProfileUnavailable;

    public StashOperationResult TryTransferToLoadout(ProfileId profileId, LootId lootId, int amount) =>
        IsProfile(profileId) ? _store.TryTransferToLoadout(lootId, amount) : StashOperationResult.InvalidInventory;

    public StashOperationResult TryTransferToStash(ProfileId profileId, LootId lootId, int amount) =>
        IsProfile(profileId) ? _store.TryTransferToStash(lootId, amount) : StashOperationResult.InvalidInventory;

    public StashOperationResult TryTransferAllToLoadout(ProfileId profileId) =>
        IsProfile(profileId) ? _store.TryTransferAllToLoadout() : StashOperationResult.InvalidInventory;

    public StashOperationResult TryTransferAllToStash(ProfileId profileId) =>
        IsProfile(profileId) ? _store.TryTransferAllToStash() : StashOperationResult.InvalidInventory;

    public StashOperationResult EvaluateTransferToLoadout(ProfileId profileId, LootId lootId, int amount) =>
        IsProfile(profileId) ? _store.EvaluateTransferToLoadout(lootId, amount) : StashOperationResult.InvalidInventory;

    public StashOperationResult EvaluateTransferToStash(ProfileId profileId, LootId lootId, int amount) =>
        IsProfile(profileId) ? _store.EvaluateTransferToStash(lootId, amount) : StashOperationResult.InvalidInventory;

    public StashOperationResult EvaluateTransferAllToLoadout(ProfileId profileId) =>
        IsProfile(profileId) ? _store.EvaluateTransferAllToLoadout() : StashOperationResult.InvalidInventory;

    public StashOperationResult EvaluateTransferAllToStash(ProfileId profileId) =>
        IsProfile(profileId) ? _store.EvaluateTransferAllToStash() : StashOperationResult.InvalidInventory;

    public StashOperationResult TryImportItems(ProfileId profileId, IReadOnlyList<StashItem> items) =>
        IsProfile(profileId) ? _store.TryImportItems(items) : StashOperationResult.InvalidInventory;

    public StashOperationResult TryCreateLoadoutReservation(
        ProfileId profileId,
        string reservationId,
        out PendingLoadoutReservation reservation)
    {
        if (!IsProfile(profileId))
        {
            reservation = null;
            return StashOperationResult.InvalidInventory;
        }

        return _store.TryCreateLoadoutReservation(reservationId, out reservation);
    }

    public StashOperationResult TryConfirmLoadoutReservation(ProfileId profileId, string reservationId) =>
        IsProfile(profileId)
            ? _store.TryConfirmLoadoutReservation(reservationId)
            : StashOperationResult.InvalidInventory;

    public StashOperationResult TryRollbackLoadoutReservation(ProfileId profileId, string reservationId) =>
        IsProfile(profileId)
            ? _store.TryRollbackLoadoutReservation(reservationId)
            : StashOperationResult.InvalidInventory;

    private bool IsProfile(ProfileId profileId) => _store != null && profileId == _store.ProfileId;

    private void OnProfileCommitted(ProfileId profileId) => LoadoutChanged?.Invoke(profileId);

    private void OnDestroy()
    {
        if (_store != null) _store.ProfileCommitted -= OnProfileCommitted;
    }
}
