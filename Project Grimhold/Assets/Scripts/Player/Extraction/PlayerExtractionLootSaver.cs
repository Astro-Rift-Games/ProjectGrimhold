using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Observes extraction completion events to securely transfer the player's raid inventory
/// to their Loadout in the Lobby. Executes exclusively on State Authority.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerExtractionController))]
[RequireComponent(typeof(PlayerLootReceiver))]
public sealed class PlayerExtractionLootSaver : NetworkBehaviour
{
    private PlayerExtractionController _extractionController;
    private PlayerLootReceiver _lootReceiver;
    private IPlayerLoadoutService _loadoutService;

    [Networked]
    private NetworkBool HasSecuredLoot { get; set; }

    private void Awake()
    {
        _extractionController = GetComponent<PlayerExtractionController>();
        _lootReceiver = GetComponent<PlayerLootReceiver>();
    }

    public override void Spawned()
    {
        var context = FindAnyObjectByType<ApplicationStashContext>();
        if (context != null)
        {
            _loadoutService = context.LoadoutService;
        }
        else
        {
            Debug.LogWarning($"{nameof(PlayerExtractionLootSaver)}: {nameof(ApplicationStashContext)} not found. Loadout service will be unavailable.", this);
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

        if (_loadoutService == null)
        {
            Debug.LogError($"{nameof(PlayerExtractionLootSaver)}: Cannot secure loot. {nameof(IPlayerLoadoutService)} is missing.", this);
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

        // Read the persistent ProfileId from the networked player character
        string profileIdValue = "UnknownProfile";
        if (TryGetComponent(out PlayerCharacter playerCharacter) && !string.IsNullOrEmpty(playerCharacter.ProfileIdString.ToString()))
        {
            profileIdValue = playerCharacter.ProfileIdString.ToString();
        }
        else
        {
            Debug.LogWarning($"[PlayerExtractionLootSaver] Could not find ProfileIdString on PlayerCharacter. Falling back to InputAuthority.", this);
            profileIdValue = Object.InputAuthority.ToString();
        }

        ProfileId profileId = new ProfileId(profileIdValue);

        StashOperationResult result = _loadoutService.TryImportItems(profileId, stashItems);

        if (result == StashOperationResult.Success)
        {
            if (_lootReceiver.TryClearExactContent(snapshot, out string clearError))
            {
                HasSecuredLoot = true;
                Debug.Log($"[PlayerExtractionLootSaver] Successfully imported {snapshot.Count} item types to loadout for profile {profileId}.", this);
            }
            else
            {
                Debug.LogError($"[PlayerExtractionLootSaver] Loot was secured, but clearing the raid inventory failed: {clearError}", this);
            }
        }
        else
        {
            Debug.LogError($"[PlayerExtractionLootSaver] Failed to secure loot to loadout. Result: {result}. Inventory will not be cleared.", this);
        }
    }
}
