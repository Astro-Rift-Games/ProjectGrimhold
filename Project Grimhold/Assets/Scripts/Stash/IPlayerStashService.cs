using System;
using System.Collections.Generic;

/// <summary>
/// Service abstraction for interacting with a player's stash.
/// Enables future replacements with persistent backends, database calls, or host-managed services.
/// </summary>
public interface IPlayerStashService
{
    /// <summary>
    /// Attempts to securely store a list of stash items for the given player profile.
    /// </summary>
    /// <param name="profileId">The profile identifier of the player for the current application run.</param>
    /// <param name="items">An immutable snapshot of items to store.</param>
    /// <returns>The result of the stash operation.</returns>
    StashOperationResult TrySecureLoot(ProfileId profileId, IReadOnlyList<StashItem> items);

    /// <summary>
    /// Retrieves the current stash items for the specified profile.
    /// </summary>
    IReadOnlyList<StashItem> GetStash(ProfileId profileId);

    /// <summary>
    /// Attempts to consume a specific amount of an item from the stash.
    /// Used by the loadout service to move items out of the stash.
    /// </summary>
    StashOperationResult TryConsumeLoot(ProfileId profileId, LootId lootId, int amount);

    /// <summary>
    /// Fired when a profile's stash has been modified.
    /// </summary>
    event Action<ProfileId> StashChanged;
}
