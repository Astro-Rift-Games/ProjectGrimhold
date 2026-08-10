using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Observes extraction completion events to securely transfer the player's raid inventory
/// to their Loadout in the Lobby via network RPC. Executes exclusively on State Authority.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerExtractionController))]
[RequireComponent(typeof(PlayerLootReceiver))]
public sealed class PlayerExtractionLootSaver : NetworkBehaviour
{
    private PlayerExtractionController _extractionController;
    private PlayerLootReceiver _lootReceiver;

    [Networked]
    private NetworkBool HasSecuredLoot { get; set; }

    private void Awake()
    {
        _extractionController = GetComponent<PlayerExtractionController>();
        _lootReceiver = GetComponent<PlayerLootReceiver>();
    }

    public override void Spawned()
    {
        // TASK-58 intentionally leaves extraction pending. The previous implementation
        // cleared authoritative raid inventory before a local persistence acknowledgement.
        // TASK-80 replaces it with an idempotent stash transaction and ACK.
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
    }

    private void HandleExtractionCompleted(PlayerExtractionController controller)
    {
        if (!HasStateAuthority || HasSecuredLoot)
        {
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

        var catalog = _lootReceiver.LootCatalog;
        if (catalog == null)
        {
            Debug.LogError($"{nameof(PlayerExtractionLootSaver)}: LootDefinitionCatalog is missing from PlayerLootReceiver.", this);
            return;
        }

        var catalogIndices = new List<int>();
        var amounts = new List<int>();

        for (int i = 0; i < snapshot.Count; i++)
        {
            if (catalog.TryGetIndex(snapshot[i].LootId, out int index))
            {
                catalogIndices.Add(index);
                amounts.Add(snapshot[i].Amount);
            }
            else
            {
                Debug.LogError($"{nameof(PlayerExtractionLootSaver)}: Could not resolve catalog index for LootId '{snapshot[i].LootId.Value}'. Item ignored.", this);
            }
        }

        if (catalogIndices.Count == 0)
        {
            HasSecuredLoot = true;
            return;
        }

        // 1. Mark as secured and clear inventory on State Authority (Server)
        if (_lootReceiver.TryClearExactContent(snapshot, out string clearError))
        {
            HasSecuredLoot = true;
            Debug.Log($"[PlayerExtractionLootSaver] Authoritatively cleared {catalogIndices.Count} loot types in raid. Sending RPC to client.", this);

            // 2. Send RPC to Input Authority (Client) to import into their local Loadout
            RPC_SecureLootOnClient(catalogIndices.ToArray(), amounts.ToArray());
        }
        else
        {
            Debug.LogError($"[PlayerExtractionLootSaver] Failed to clear raid inventory: {clearError}. Loot will not be secured on client.", this);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_SecureLootOnClient(int[] catalogIndices, int[] amounts)
    {
        if (catalogIndices == null || amounts == null || catalogIndices.Length != amounts.Length)
        {
            Debug.LogError($"[PlayerExtractionLootSaver] RPC received with invalid payload.", this);
            return;
        }

        var catalog = _lootReceiver.LootCatalog;
        if (catalog == null)
        {
            Debug.LogError($"[PlayerExtractionLootSaver] Client LootDefinitionCatalog is missing.", this);
            return;
        }

        var items = new List<StashItem>(catalogIndices.Length);
        for (int i = 0; i < catalogIndices.Length; i++)
        {
            if (catalog.TryGetByIndex(catalogIndices[i], out LootDefinition definition))
            {
                items.Add(new StashItem(definition.LootId, amounts[i]));
            }
            else
            {
                Debug.LogError($"[PlayerExtractionLootSaver] Client failed to resolve catalog index {catalogIndices[i]}", this);
            }
        }

        if (items.Count == 0)
        {
            return;
        }

        ProfileId localProfileId = LocalProfileProvider.GetOrCreateLocalProfile();
        
        var context = FindAnyObjectByType<ApplicationStashContext>();
        if (context != null && context.LoadoutService != null)
        {
            StashOperationResult result = context.LoadoutService.TryImportItems(localProfileId, items);
            if (result == StashOperationResult.Success)
            {
                Debug.Log($"[PlayerExtractionLootSaver] Client successfully secured {items.Count} items to local loadout.", this);
            }
            else
            {
                Debug.LogError($"[PlayerExtractionLootSaver] Client failed to secure items to local loadout. Result: {result}", this);
            }
        }
        else
        {
            Debug.LogError($"[PlayerExtractionLootSaver] Client ApplicationStashContext or LoadoutService not found. Extracted items were lost!", this);
        }
    }
}
