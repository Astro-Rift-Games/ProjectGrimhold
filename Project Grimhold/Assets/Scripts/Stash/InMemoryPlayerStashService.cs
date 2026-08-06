using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary in-memory implementation of <see cref="IPlayerStashService"/>.
/// Stores accumulated stash items for Stage 1. Does not persist across application sessions.
/// </summary>
public sealed class InMemoryPlayerStashService : MonoBehaviour, IPlayerStashService
{
    private readonly Dictionary<ProfileId, List<StashItem>> _stashes = new();

    public StashOperationResult TrySecureLoot(ProfileId profileId, IReadOnlyList<StashItem> items)
    {
        if (!profileId.IsValid)
        {
            Debug.LogError($"{nameof(InMemoryPlayerStashService)}: ProfileId is invalid.");
            return StashOperationResult.PersistenceFailed;
        }

        if (items == null || items.Count == 0)
        {
            return StashOperationResult.InvalidInventory;
        }

        if (!_stashes.TryGetValue(profileId, out List<StashItem> currentStash))
        {
            currentStash = new List<StashItem>();
            _stashes[profileId] = currentStash;
        }

        for (int i = 0; i < items.Count; i++)
        {
            StashItem incomingItem = items[i];
            if (!incomingItem.IsValid)
            {
                continue;
            }

            bool merged = false;
            for (int j = 0; j < currentStash.Count; j++)
            {
                StashItem existingItem = currentStash[j];
                // For Stage 1, we merge by LootId. 
                // Later stages with instance-specific state may change merging logic.
                if (existingItem.LootId == incomingItem.LootId)
                {
                    currentStash[j] = new StashItem(existingItem.LootId, existingItem.Amount + incomingItem.Amount);
                    merged = true;
                    break;
                }
            }

            if (!merged)
            {
                currentStash.Add(incomingItem);
            }
        }

        return StashOperationResult.Success;
    }
}
