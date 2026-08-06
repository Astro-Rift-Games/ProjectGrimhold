using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Observes extraction completion events to securely transfer the player's raid inventory
/// to their persistent stash. Executes exclusively on State Authority.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerExtractionController))]
[RequireComponent(typeof(PlayerLootReceiver))]
public sealed class PlayerExtractionLootSaver : NetworkBehaviour
{
    private PlayerExtractionController _extractionController;
    private PlayerLootReceiver _lootReceiver;
    private IPlayerStashService _stashService;

    [Networked]
    private NetworkBool HasSecuredLoot { get; set; }

    private void Awake()
    {
        _extractionController = GetComponent<PlayerExtractionController>();
        _lootReceiver = GetComponent<PlayerLootReceiver>();
    }

    public override void Spawned()
    {
        _stashService = Runner.GetComponent<IPlayerStashService>();
        if (_stashService == null)
        {
            // Fallback for Stage 1 if not attached to Runner
            _stashService = FindAnyObjectByType<InMemoryPlayerStashService>();
            if (_stashService == null)
            {
                Debug.LogWarning($"{nameof(PlayerExtractionLootSaver)}: No {nameof(IPlayerStashService)} found in the scene or on the Runner.", this);
            }
        }

        if (_extractionController != null)
        {
            _extractionController.ExtractionCompleted += HandleExtractionCompleted;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_extractionController != null)
        {
            _extractionController.ExtractionCompleted -= HandleExtractionCompleted;
        }
    }

    private void HandleExtractionCompleted(PlayerExtractionController controller)
    {
        if (!HasStateAuthority || HasSecuredLoot)
        {
            return;
        }

        if (_stashService == null)
        {
            Debug.LogError($"{nameof(PlayerExtractionLootSaver)}: Cannot secure loot. {nameof(IPlayerStashService)} is missing.", this);
            return;
        }

        if (!_lootReceiver.TryGetLootContent(out IReadOnlyList<LootEntry> snapshot))
        {
            Debug.LogError($"{nameof(PlayerExtractionLootSaver)}: Failed to capture inventory snapshot.", this);
            return;
        }

        if (snapshot.Count == 0)
        {
            HasSecuredLoot = true;
            return;
        }

        // Map LootEntry to StashItem
        var stashItems = new StashItem[snapshot.Count];
        for (int i = 0; i < snapshot.Count; i++)
        {
            stashItems[i] = new StashItem(snapshot[i].LootId, snapshot[i].Amount);
        }

        // Generate a ProfileId. In the future this should come from a real account/profile system.
        ProfileId profileId = new ProfileId(Object.InputAuthority.ToString());

        StashOperationResult result = _stashService.TrySecureLoot(profileId, stashItems);

        if (result == StashOperationResult.Success || result == StashOperationResult.AlreadySecured)
        {
            if (_lootReceiver.TryClearExactContent(snapshot, out string clearError))
            {
                HasSecuredLoot = true;
                Debug.Log($"[PlayerExtractionLootSaver] Successfully secured {snapshot.Count} item types to stash for profile {profileId}.", this);
            }
            else
            {
                Debug.LogError($"[PlayerExtractionLootSaver] Loot was secured, but clearing the raid inventory failed: {clearError}", this);
            }
        }
        else
        {
            Debug.LogError($"[PlayerExtractionLootSaver] Failed to secure loot. Result: {result}. Inventory will not be cleared.", this);
        }
    }
}
