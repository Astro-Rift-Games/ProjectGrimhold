using System;
using System.Collections.Generic;

/// <summary>
/// Service abstraction for interacting with a player's pre-match loadout.
/// Handles transfers between the application-level stash and the active loadout configuration.
/// Serves as the authority for loadout limits and validation.
/// </summary>
public interface IPlayerLoadoutService
{
    /// <summary>
    /// Retrieves the current active loadout for the specified profile.
    /// </summary>
    IReadOnlyList<StashItem> GetLoadout(ProfileId profileId);

    PreparedEquipmentLoadout GetPreparedEquipment(ProfileId profileId);

    /// <summary>
    /// Assigns one owned unit atomically. Weapon assignments must satisfy the profile's confirmed
    /// character attributes; armor assignments and slot release have no attribute requirement.
    /// </summary>
    StashOperationResult TryAssignPreparedEquipment(
        ProfileId profileId,
        EquipmentSlot slot,
        LootId lootId);

    StashOperationResult TryClearPreparedEquipment(ProfileId profileId, EquipmentSlot slot);

    /// <summary>
    /// Atomically removes one Stash unit into Equipment and returns displaced units to Loadout.
    /// </summary>
    StashOperationResult TryEquipFromStash(ProfileId profileId, LootId lootId, EquipmentSlot slot);

    /// <summary>Projects the complete Stash equip without mutation, validating final Loadout capacity.</summary>
    StashOperationResult EvaluateEquipFromStash(
        ProfileId profileId, LootId lootId, EquipmentSlot slot, out PreparedEquipmentLoadout equipment);

    /// <summary>
    /// Normalizes the Loadout and prepared Weapon Equipment so a raid reservation can succeed.
    /// It must run before <see cref="TryCreateLoadoutReservation"/> and is safe to retry.
    /// </summary>
    ExpeditionPreparationResult TryPrepareExpeditionLoadout(ProfileId profileId);

    /// <summary>
    /// Attempts to transfer a specific amount of an item from the Stash to the Loadout.
    /// </summary>
    StashOperationResult TryTransferToLoadout(ProfileId profileId, LootId lootId, int amount);

    /// <summary>
    /// Attempts to transfer a specific amount of an item from the Loadout back to the Stash.
    /// </summary>
    StashOperationResult TryTransferToStash(ProfileId profileId, LootId lootId, int amount);

    /// <summary>
    /// Attempts to transfer all items from the Stash to the Loadout, respecting capacity.
    /// </summary>
    StashOperationResult TryTransferAllToLoadout(ProfileId profileId);

    /// <summary>
    /// Attempts to transfer all items from the Loadout to the Stash.
    /// </summary>
    StashOperationResult TryTransferAllToStash(ProfileId profileId);

    /// <summary>
    /// Reports what <see cref="TryTransferToLoadout"/> would return right now without changing or
    /// publishing anything, so callers can reject an action before it reaches the backend.
    /// </summary>
    StashOperationResult EvaluateTransferToLoadout(ProfileId profileId, LootId lootId, int amount);

    /// <summary>Dry run of <see cref="TryTransferToStash"/>.</summary>
    StashOperationResult EvaluateTransferToStash(ProfileId profileId, LootId lootId, int amount);

    /// <summary>Dry run of <see cref="TryTransferAllToLoadout"/>.</summary>
    StashOperationResult EvaluateTransferAllToLoadout(ProfileId profileId);

    /// <summary>Dry run of <see cref="TryTransferAllToStash"/>.</summary>
    StashOperationResult EvaluateTransferAllToStash(ProfileId profileId);

    /// <summary>
    /// Generic method to import items into the Loadout from an external source (e.g. extraction).
    /// Enforces loadout capacity limits explicitly.
    /// </summary>
    StashOperationResult TryImportItems(ProfileId profileId, IReadOnlyList<StashItem> items);

    /// <summary>
    /// Creates or reuses the current application reservation for a raid admission after
    /// revalidating every prepared weapon against the profile's confirmed attributes.
    /// </summary>
    StashOperationResult TryCreateLoadoutReservation(
        ProfileId profileId,
        string reservationId,
        out PendingLoadoutReservation reservation);

    /// <summary>Confirms that the reserved loadout was admitted to the raid.</summary>
    StashOperationResult TryConfirmLoadoutReservation(ProfileId profileId, string reservationId);

    /// <summary>Restores a reservation that never reached raid admission.</summary>
    StashOperationResult TryRollbackLoadoutReservation(ProfileId profileId, string reservationId);

    /// <summary>
    /// Fired when a profile's loadout has been modified.
    /// </summary>
    event Action<ProfileId> LoadoutChanged;
}
