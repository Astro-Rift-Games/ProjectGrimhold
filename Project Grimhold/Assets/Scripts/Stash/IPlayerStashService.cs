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
    /// <param name="profileId">The persistent profile identifier of the player.</param>
    /// <param name="items">An immutable snapshot of items to store.</param>
    /// <returns>The result of the stash operation.</returns>
    StashOperationResult TrySecureLoot(ProfileId profileId, IReadOnlyList<StashItem> items);
}
