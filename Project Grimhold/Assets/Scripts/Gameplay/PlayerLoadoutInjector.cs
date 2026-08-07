using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// NetworkBehaviour attached to the Player prefab.
/// When the player spawns on State Authority, it retrieves the prepared loadout 
/// from the application context and injects it into the PlayerLootReceiver.
/// This cleanly decouples the NetworkSpawnManager from the Stash/Loadout system.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerLootReceiver))]
[RequireComponent(typeof(PlayerCharacter))]
public class PlayerLoadoutInjector : NetworkBehaviour
{
    public override void Spawned()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        var context = FindAnyObjectByType<ApplicationStashContext>();
        if (context == null || context.LoadoutService == null)
        {
            Debug.LogWarning($"[{nameof(PlayerLoadoutInjector)}] ApplicationStashContext or LoadoutService not found. Loadout will be empty.");
            return;
        }

        var playerCharacter = GetComponent<PlayerCharacter>();
        string profileIdValue = playerCharacter.ProfileIdString.ToString();
        if (string.IsNullOrEmpty(profileIdValue))
        {
            Debug.LogWarning($"[{nameof(PlayerLoadoutInjector)}] ProfileIdString is empty on PlayerCharacter. Cannot fetch loadout.");
            return;
        }

        ProfileId profileId = new ProfileId(profileIdValue);
        
        // Consume the loadout, getting an immutable snapshot and clearing the active loadout state
        IReadOnlyList<LootEntry> initialLoot = context.LoadoutService.ConsumeLoadoutForRaid(profileId);

        if (initialLoot != null && initialLoot.Count > 0)
        {
            var lootReceiver = GetComponent<PlayerLootReceiver>();
            lootReceiver.InitializeLoadout(initialLoot);
        }
    }
}
