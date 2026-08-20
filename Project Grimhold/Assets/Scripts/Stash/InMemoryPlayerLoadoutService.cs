using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Compatibility adapter exposing process-local loadout operations through the
/// existing service contract. It owns no loadout state.
/// </summary>
public sealed class InMemoryPlayerLoadoutService : MonoBehaviour, IPlayerLoadoutService
{
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
