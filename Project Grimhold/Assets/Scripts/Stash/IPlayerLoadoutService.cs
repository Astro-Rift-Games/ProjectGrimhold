using System;
using System.Collections.Generic;

/// <summary>
/// Service abstraction for interacting with a player's pre-match loadout.
/// Handles transfers between the persistent stash and the active loadout configuration.
/// Serves as the authority for loadout limits and validation.
/// </summary>
public interface IPlayerLoadoutService
{
    /// <summary>
    /// Retrieves the current active loadout for the specified profile.
    /// </summary>
    IReadOnlyList<StashItem> GetLoadout(ProfileId profileId);

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
    /// Generic method to import items into the Loadout from an external source (e.g. extraction).
    /// Enforces loadout capacity limits explicitly.
    /// </summary>
    StashOperationResult TryImportItems(ProfileId profileId, IReadOnlyList<StashItem> items);

    /// <summary>Creates or reuses the durable reservation for a raid admission.</summary>
    StashOperationResult TryCreateLoadoutReservation(
        ProfileId profileId,
        string reservationId,
        out IReadOnlyList<StashItem> items);

    /// <summary>Confirms that the reserved loadout was admitted to the raid.</summary>
    StashOperationResult TryConfirmLoadoutReservation(ProfileId profileId, string reservationId);

    /// <summary>Restores a reservation that never reached raid admission.</summary>
    StashOperationResult TryRollbackLoadoutReservation(ProfileId profileId, string reservationId);

    /// <summary>
    /// Fired when a profile's loadout has been modified.
    /// </summary>
    event Action<ProfileId> LoadoutChanged;
}
