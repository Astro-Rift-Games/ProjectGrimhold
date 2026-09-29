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

    /// <summary>
    /// Commits the ticket locally, then synchronizes it with the backend. The backend only exposes
    /// single-item buy and sell endpoints, so a ticket with more than one line is refused before any
    /// mutation: sending several calls would not be atomic remotely. Multi-line tickets need an
    /// atomic backend trade endpoint (follow-up).
    /// </summary>
    public StashOperationResult TryExecuteTrade(ProfileId profileId, MerchantTradeTicket ticket)
    {
        if (_store == null) return StashOperationResult.PersistenceFailed;
        if (!IsProfile(profileId) || ticket == null) return StashOperationResult.InvalidInventory;
        if (ticket.LineCount != 1)
        {
            Debug.LogError($"[RemoteShopTransactionService] Trade {ticket.TransactionId.Value:N} has {ticket.LineCount} lines; the backend has no atomic multi-line trade endpoint.");
            return StashOperationResult.PersistenceFailed;
        }

        // 1. Attempt local validation & optimistic mutation first
        StashOperationResult localResult = _store.TryCommitTrade(profileId, ticket);
        if (localResult != StashOperationResult.Success)
        {
            return localResult; // E.g., InvalidInventory (insufficient funds), AlreadyApplied
        }

        // 2. Fire-and-forget the backend synchronization wrapped in the remote retry policy.
        // The RemoteOperationPolicy will retry on REVISION_CONFLICT / TransportFailure.
        // If it ultimately fails, it triggers a full ProfileReconciliationService sync which will revert the local optimistic state.
        string transactionId = ticket.TransactionId.Value.ToString("N");
        _ = ticket.Purchases.Count == 1
            ? ExecutePurchaseAsync(transactionId, ticket.Purchases[0])
            : ExecuteSaleAsync(transactionId, ticket.Sales[0]);

        return StashOperationResult.Success;
    }

    /// <summary>
    /// Reads the local aggregate, which the optimistic commit updates before backend sync.
    /// </summary>
    public bool IsTradeApplied(ProfileId profileId, ShopTransactionId transactionId)
    {
        return _store != null && _store.IsTradeApplied(profileId, transactionId);
    }

    private async Task ExecutePurchaseAsync(string transactionId, MerchantPricedTradeLine line)
    {
        await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
            async () =>
            {
                var (success, data, error) = await InventoryClient.ShopBuyAsync(
                    _config, 
                    AuthToken, 
                    transactionId,
                    line.LootId.Value, 
                    line.Amount, 
                    line.Total, 
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

    private async Task ExecuteSaleAsync(string transactionId, MerchantPricedTradeLine line)
    {
        await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
            async () =>
            {
                var (success, data, error) = await InventoryClient.ShopSellAsync(
                    _config, 
                    AuthToken, 
                    transactionId,
                    line.LootId.Value, 
                    line.Amount, 
                    line.Total, 
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
