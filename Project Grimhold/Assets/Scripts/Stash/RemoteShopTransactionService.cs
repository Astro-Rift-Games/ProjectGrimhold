using UnityEngine;
using System.Threading.Tasks;
using Grimhold.Backend;
using System.Threading;

/// <summary>
/// Remote implementation of the shop transaction service.
/// Validates transactions locally first, then delegates to the backend for authority.
/// Retries on REVISION_CONFLICT and network errors via RemoteOperationPolicy.
/// </summary>
public sealed class RemoteShopTransactionService : MonoBehaviour, IShopTransactionService
{
    private LocalProfileStore _store;
    private BackendConfiguration _config;
    private ProfileReconciliationService _reconciliationService;
    private string AuthToken => ApplicationAuthContext.Instance?.Token;

    public void Initialize(
        LocalProfileStore store,
        LocalProfilePersistenceConfiguration localConfig,
        ProfileReconciliationService reconciliationService)
    {
        _store = store ?? throw new System.ArgumentNullException(nameof(store));
        
        if (LoginFlowController.Instance != null && LoginFlowController.Instance.Config != null)
        {
            _config = LoginFlowController.Instance.Config;
        }
        else
        {
            _config = Resources.Load<BackendConfiguration>("BackendConfiguration");
            if (_config == null) _config = ScriptableObject.CreateInstance<BackendConfiguration>();
        }

        _reconciliationService = reconciliationService ?? throw new System.ArgumentNullException(nameof(reconciliationService));
    }

    public StashOperationResult TryExecutePurchase(ProfileId profileId, LootId lootId, int amount, long declaredPrice, ShopTransactionId transactionId)
    {
        if (_store == null) return StashOperationResult.PersistenceFailed;
        if (!IsProfile(profileId)) return StashOperationResult.InvalidInventory;

        var receipt = new ShopTransactionReceipt(transactionId, profileId);
        
        // 1. Attempt local validation & optimistic mutation first
        bool isLobby = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Lobby");
        StashOperationResult localResult = _store.TryCommitPurchase(receipt, lootId, amount, declaredPrice, isLobby);
        
        if (localResult != StashOperationResult.Success)
        {
            return localResult; // E.g., InvalidInventory (insufficient funds), AlreadyApplied
        }

        // 2. Fire-and-forget the backend synchronization wrapped in the remote retry policy.
        // The RemoteOperationPolicy will retry on REVISION_CONFLICT / TransportFailure.
        // If it ultimately fails, it triggers a full ProfileReconciliationService sync which will revert the local optimistic state.
        _ = ExecutePurchaseAsync(receipt, lootId, amount, declaredPrice);

        return StashOperationResult.Success;
    }

    private async Task ExecutePurchaseAsync(ShopTransactionReceipt receipt, LootId lootId, int amount, long declaredPrice)
    {
        await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
            async () =>
            {
                var (success, data, error) = await InventoryClient.ShopBuyAsync(
                    _config, 
                    AuthToken, 
                    lootId.Value, 
                    amount, 
                    declaredPrice, 
                    _store.RemoteRevision);

                if (success)
                {
                    _store.SetRemoteRevision(data.revision);
                }

                return (success, error);
            },
            () => _reconciliationService.ReconcileAsync()
        );
    }

    public StashOperationResult TryExecuteSale(ProfileId profileId, LootId lootId, int amount, long declaredSellValue, ShopTransactionId transactionId)
    {
        if (_store == null) return StashOperationResult.PersistenceFailed;
        if (!IsProfile(profileId)) return StashOperationResult.InvalidInventory;

        var receipt = new ShopTransactionReceipt(transactionId, profileId);

        // 1. Attempt local validation & optimistic mutation first
        bool isLobby = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Lobby");
        StashOperationResult localResult = _store.TryCommitSale(receipt, lootId, amount, declaredSellValue, isLobby);
        
        if (localResult != StashOperationResult.Success)
        {
            return localResult; 
        }

        // 2. Fire-and-forget the backend synchronization.
        _ = ExecuteSaleAsync(receipt, lootId, amount, declaredSellValue);

        return StashOperationResult.Success;
    }

    private async Task ExecuteSaleAsync(ShopTransactionReceipt receipt, LootId lootId, int amount, long declaredSellValue)
    {
        await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
            async () =>
            {
                var (success, data, error) = await InventoryClient.ShopSellAsync(
                    _config, 
                    AuthToken, 
                    lootId.Value, 
                    amount, 
                    declaredSellValue, 
                    _store.RemoteRevision);

                if (success)
                {
                    _store.SetRemoteRevision(data.revision);
                }

                return (success, error);
            },
            () => _reconciliationService.ReconcileAsync()
        );
    }

    private bool IsProfile(ProfileId profileId)
    {
        return profileId.IsValid && _store.ProfileId == profileId;
    }
}
