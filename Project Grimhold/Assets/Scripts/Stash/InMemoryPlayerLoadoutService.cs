using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary in-memory implementation of <see cref="IPlayerLoadoutService"/>.
/// Validates transfers against the <see cref="IPlayerStashService"/> and enforces a 16-slot maximum capacity.
/// </summary>
public sealed class InMemoryPlayerLoadoutService : MonoBehaviour, IPlayerLoadoutService
{
    private const int MaxLoadoutSlots = 16;
    private readonly Dictionary<ProfileId, List<StashItem>> _loadouts = new();
    private IPlayerStashService _stashService;

    public event Action<ProfileId> LoadoutChanged;

    private void Start()
    {
        var context = FindAnyObjectByType<ApplicationStashContext>();
        if (context != null)
        {
            _stashService = context.StashService;
        }
    }

    public IReadOnlyList<StashItem> GetLoadout(ProfileId profileId)
    {
        if (profileId.IsValid && _loadouts.TryGetValue(profileId, out var loadout))
        {
            return loadout;
        }
        return Array.Empty<StashItem>();
    }

    public StashOperationResult TryTransferToLoadout(ProfileId profileId, LootId lootId, int amount)
    {
        if (!profileId.IsValid || !lootId.IsValid || amount <= 0 || _stashService == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        // Verify stash has enough amount
        var stash = _stashService.GetStash(profileId);
        int availableInStash = 0;
        foreach (var item in stash)
        {
            if (item.LootId == lootId)
            {
                availableInStash = item.Amount;
                break;
            }
        }

        if (availableInStash == 0)
        {
            return StashOperationResult.InvalidInventory;
        }

        int actualAmount = Mathf.Min(amount, availableInStash);

        // Get or create loadout
        if (!_loadouts.TryGetValue(profileId, out List<StashItem> currentLoadout))
        {
            currentLoadout = new List<StashItem>();
            _loadouts[profileId] = currentLoadout;
        }

        // Check if item already exists in loadout
        int loadoutIndex = -1;
        for (int i = 0; i < currentLoadout.Count; i++)
        {
            if (currentLoadout[i].LootId == lootId)
            {
                loadoutIndex = i;
                break;
            }
        }

        // Validate capacity if it's a new item
        if (loadoutIndex == -1 && currentLoadout.Count >= MaxLoadoutSlots)
        {
            Debug.LogWarning($"[InMemoryPlayerLoadoutService] Cannot add {lootId} for {profileId.Value}. Loadout is full.");
            return StashOperationResult.PersistenceFailed; // TODO: Use a more specific error like CapacityReached
        }

        var removeResult = _stashService.TryConsumeLoot(profileId, lootId, actualAmount);
        if (removeResult != StashOperationResult.Success)
        {
            return removeResult;
        }

        // Add to Loadout
        if (loadoutIndex != -1)
        {
            currentLoadout[loadoutIndex] = new StashItem(lootId, currentLoadout[loadoutIndex].Amount + actualAmount);
        }
        else
        {
            currentLoadout.Add(new StashItem(lootId, actualAmount));
        }

        LoadoutChanged?.Invoke(profileId);
        return StashOperationResult.Success;
    }

    public StashOperationResult TryTransferToStash(ProfileId profileId, LootId lootId, int amount)
    {
        if (!profileId.IsValid || !lootId.IsValid || amount <= 0 || _stashService == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        if (!_loadouts.TryGetValue(profileId, out List<StashItem> currentLoadout))
        {
            return StashOperationResult.InvalidInventory;
        }

        int loadoutIndex = -1;
        for (int i = 0; i < currentLoadout.Count; i++)
        {
            if (currentLoadout[i].LootId == lootId)
            {
                loadoutIndex = i;
                break;
            }
        }

        if (loadoutIndex == -1)
        {
            return StashOperationResult.InvalidInventory;
        }

        int actualAmount = Mathf.Min(amount, currentLoadout[loadoutIndex].Amount);

        // Add to stash
        var stashItems = new[] { new StashItem(lootId, actualAmount) };
        var secureResult = _stashService.TrySecureLoot(profileId, stashItems);
        if (secureResult != StashOperationResult.Success)
        {
            return secureResult;
        }

        // Remove from loadout
        int newAmount = currentLoadout[loadoutIndex].Amount - actualAmount;
        if (newAmount > 0)
        {
            currentLoadout[loadoutIndex] = new StashItem(lootId, newAmount);
        }
        else
        {
            currentLoadout.RemoveAt(loadoutIndex);
        }

        LoadoutChanged?.Invoke(profileId);
        return StashOperationResult.Success;
    }

    public StashOperationResult TryTransferAllToLoadout(ProfileId profileId)
    {
        if (!profileId.IsValid || _stashService == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        var stash = _stashService.GetStash(profileId);
        if (stash == null || stash.Count == 0)
        {
            return StashOperationResult.Success;
        }

        // Process a copy because stash might change during consumption
        var itemsToTransfer = new List<StashItem>(stash);
        bool anySuccess = false;

        foreach (var item in itemsToTransfer)
        {
            var result = TryTransferToLoadout(profileId, item.LootId, item.Amount);
            if (result == StashOperationResult.Success)
            {
                anySuccess = true;
            }
        }

        return anySuccess ? StashOperationResult.Success : StashOperationResult.PersistenceFailed;
    }

    public StashOperationResult TryTransferAllToStash(ProfileId profileId)
    {
        if (!profileId.IsValid || _stashService == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        if (!_loadouts.TryGetValue(profileId, out List<StashItem> currentLoadout) || currentLoadout.Count == 0)
        {
            return StashOperationResult.Success;
        }

        // Process a copy because loadout will change during transfer
        var itemsToTransfer = new List<StashItem>(currentLoadout);
        bool anySuccess = false;

        foreach (var item in itemsToTransfer)
        {
            var result = TryTransferToStash(profileId, item.LootId, item.Amount);
            if (result == StashOperationResult.Success)
            {
                anySuccess = true;
            }
        }

        return anySuccess ? StashOperationResult.Success : StashOperationResult.PersistenceFailed;
    }

    public IReadOnlyList<LootEntry> ConsumeLoadoutForRaid(ProfileId profileId)
    {
        if (!profileId.IsValid || !_loadouts.TryGetValue(profileId, out List<StashItem> currentLoadout))
        {
            return Array.Empty<LootEntry>();
        }

        var snapshot = new List<LootEntry>(currentLoadout.Count);
        for (int i = 0; i < currentLoadout.Count; i++)
        {
            snapshot.Add(new LootEntry(currentLoadout[i].LootId, currentLoadout[i].Amount));
        }

        // Clear the loadout since it's now injected into the raid
        currentLoadout.Clear();
        LoadoutChanged?.Invoke(profileId);

        return snapshot.AsReadOnly();
    }
}
