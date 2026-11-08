using System.Threading.Tasks;
using UnityEngine;
using Grimhold.Backend;

/// <summary>
/// Service responsible for fetching authoritative state from the backend and applying it to the LocalProfileStore
/// when a revision conflict is detected.
/// </summary>
public class ProfileReconciliationService : MonoBehaviour, IProfileReconciliation, IRecoveryWeaponAuthorityReader
{
    private BackendConfiguration _config;
    private LocalProfilePersistenceConfiguration _localConfig;
    private LocalProfileStore _store;
    private BuybackLedger _buybackLedger;

    private string AuthToken => ApplicationAuthContext.Instance?.Token;

    public void Initialize(LocalProfilePersistenceConfiguration localConfig, LocalProfileStore store, BuybackLedger buybackLedger = null)
    {
        _localConfig = localConfig;
        _store = store;
        _buybackLedger = buybackLedger;
        
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
        RecoveryWeaponAuthorityState state = await FetchRecoveryWeaponAuthorityAsync();
        if (state == null) return false;
        var result = _store.ReconcileRemoteState(state.Inventory, state.Progression,
            _localConfig.LootCatalog, _localConfig.AbilityCatalog);
        if (result != StashOperationResult.Success)
        {
            Debug.LogError($"[ProfileReconciliationService] ReconcileRemoteState failed with result: {result}");
            return false;
        }
        _buybackLedger?.Replace(state.Inventory.buyback);
        return true;
    }

    /// <summary>Fetches durable extraction instances before atomically retiring the local retry.</summary>
    public async Task<bool> CompleteExtractionAsync(ExtractionReceipt receipt, int acknowledgedRevision)
    {
        string token = AuthToken;
        try
        {
            RecoveryWeaponAuthorityState state = await FetchRecoveryWeaponAuthorityAsync();
            if (state == null || token != AuthToken || _store == null || _localConfig == null)
            {
                Debug.LogError("[ProfileReconciliationService] Extraction hydration fetch failed or session changed. Pending extraction retained for retry.");
                return false;
            }
            StashOperationResult result = _store.TryCompleteExtractionCommit(
                receipt, acknowledgedRevision, state.Inventory, state.Progression);
            if (result != StashOperationResult.Success)
            {
                Debug.LogError($"[ProfileReconciliationService] Extraction hydration failed: {result}. Pending extraction retained for retry.");
                return false;
            }
            _buybackLedger?.Replace(state.Inventory.buyback);
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[ProfileReconciliationService] Extraction hydration failed: {exception.Message}. Pending extraction retained for retry.");
            return false;
        }
    }

    /// <summary>Shares ordinary reconciliation's consistent full-read boundary without applying it.</summary>
    public async Task<RecoveryWeaponAuthorityState> FetchRecoveryWeaponAuthorityAsync()
    {
        if (string.IsNullOrEmpty(AuthToken) || _store == null || _localConfig == null)
        {
            Debug.LogError("[ProfileReconciliationService] Not initialized or not authenticated.");
            return null;
        }

        var inventoryTask = InventoryClient.GetInventoryAsync(_config, AuthToken);
        var progressionTask = ProgressionClient.GetProgressionAsync(_config, AuthToken);

        await Task.WhenAll(inventoryTask, progressionTask);

        var invResult = inventoryTask.Result;
        var progResult = progressionTask.Result;

        if (invResult.success && progResult.success && invResult.data.revision != progResult.data.revision)
        {
            Debug.LogWarning($"[ProfileReconciliationService] Mismatch in remote revisions during reconciliation: Inv({invResult.data.revision}) vs Prog({progResult.data.revision}). Retrying once...");
            inventoryTask = InventoryClient.GetInventoryAsync(_config, AuthToken);
            progressionTask = ProgressionClient.GetProgressionAsync(_config, AuthToken);
            await Task.WhenAll(inventoryTask, progressionTask);
            invResult = inventoryTask.Result;
            progResult = progressionTask.Result;
        }

        var (invOk, invData, invErr) = invResult;
        var (progOk, progData, progErr) = progResult;

        if ((!invOk && invErr.error == BackendErrorUtility.Unauthorized) ||
            (!progOk && progErr.error == BackendErrorUtility.Unauthorized))
        {
            ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
            return null;
        }

        if (!invOk || !progOk)
        {
            Debug.LogError("[ProfileReconciliationService] Failed to fetch state for reconciliation.");
            return null;
        }

        if (invData.revision != progData.revision)
        {
            Debug.LogError($"[ProfileReconciliationService] Mismatch in remote revisions after retry: Inv({invData.revision}) vs Prog({progData.revision})");
            return null; // Still mismatching, can't safely reconcile
        }

        return new RecoveryWeaponAuthorityState(invData, progData);
    }
}
