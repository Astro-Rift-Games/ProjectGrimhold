using System.Threading.Tasks;
using UnityEngine;
using Grimhold.Backend;

/// <summary>
/// Service responsible for fetching authoritative state from the backend and applying it to the LocalProfileStore
/// when a revision conflict is detected.
/// </summary>
public class ProfileReconciliationService : MonoBehaviour
{
    private BackendConfiguration _config;
    private LocalProfilePersistenceConfiguration _localConfig;
    private LocalProfileStore _store;

    private string AuthToken => ApplicationAuthContext.Instance?.Token;

    public void Initialize(LocalProfilePersistenceConfiguration localConfig, LocalProfileStore store)
    {
        _localConfig = localConfig;
        _store = store;
        
        if (LoginFlowController.Instance != null && LoginFlowController.Instance.Config != null)
        {
            _config = LoginFlowController.Instance.Config;
        }
        else
        {
            _config = Resources.Load<BackendConfiguration>("BackendConfiguration");
            if (_config == null)
            {
                _config = ScriptableObject.CreateInstance<BackendConfiguration>();
            }
        }
    }

    public async Task<bool> ReconcileAsync()
    {
        if (string.IsNullOrEmpty(AuthToken) || _store == null || _localConfig == null)
        {
            Debug.LogError("[ProfileReconciliationService] Not initialized or not authenticated.");
            return false;
        }

        var inventoryTask = InventoryClient.GetInventoryAsync(_config, AuthToken);
        var progressionTask = ProgressionClient.GetProgressionAsync(_config, AuthToken);

        await Task.WhenAll(inventoryTask, progressionTask);

        var (invOk, invData, _) = inventoryTask.Result;
        var (progOk, progData, _) = progressionTask.Result;

        if (!invOk || !progOk)
        {
            Debug.LogError("[ProfileReconciliationService] Failed to fetch state for reconciliation.");
            return false;
        }

        if (invData.revision != progData.revision)
        {
            Debug.LogError($"[ProfileReconciliationService] Mismatch in remote revisions during reconciliation: Inv({invData.revision}) vs Prog({progData.revision})");
            return false; // Still mismatching, can't safely reconcile
        }

        // Use catalogs from local configuration instead of Resources.Load
        var catalog = _localConfig.LootCatalog;
        var abilityCatalog = _localConfig.AbilityCatalog;

        // Apply authoritative data directly to the local store
        var result = _store.ReconcileRemoteState(invData, progData, catalog, abilityCatalog);
        
        if (result != StashOperationResult.Success)
        {
            Debug.LogError($"[ProfileReconciliationService] ReconcileRemoteState failed with result: {result}");
            return false;
        }
        
        return true;
    }
}
