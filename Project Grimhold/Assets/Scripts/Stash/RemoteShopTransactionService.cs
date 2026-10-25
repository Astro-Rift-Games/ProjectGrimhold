using System;
using System.Threading.Tasks;
using Grimhold.Backend;
using UnityEngine;

/// <summary>
/// Remote-first shop persistence boundary: dry run, await backend acknowledgement, then commit
/// the local receipt and revision. No automatic retries, including revision conflicts.
/// A lost transport response can hide a persisted trade; reconciliation converges the profile and
/// the backend receipt makes a later retry with the same transaction id safe.
/// </summary>
public sealed class RemoteShopTransactionService : MonoBehaviour, IShopTransactionService
{
    // One infrastructure seam for the two single-line endpoints; no separate commerce transport.
    public delegate Task<(bool success, ShopTransactionResult data, BackendError error)> BackendCall(
        bool purchase, string transactionId, MerchantPricedTradeLine line, int expectedRevision);

    private LocalProfileStore _store;
    private BackendCall _backend;
    private Func<Task<bool>> _reconcile;

    public void Initialize(LocalProfileStore store, LocalProfilePersistenceConfiguration localConfig,
        ProfileReconciliationService reconciliationService)
    {
        if (reconciliationService == null) throw new ArgumentNullException(nameof(reconciliationService));
        BackendConfiguration config = LoginFlowController.Instance != null ? LoginFlowController.Instance.Config : null;
        if (config == null) config = Resources.Load<BackendConfiguration>("BackendConfiguration");
        if (config == null)
        {
            Debug.LogError("[RemoteShopTransactionService] BackendConfiguration is missing; commerce is unavailable.");
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _backend = null;
            return;
        }

        Initialize(store, (purchase, transactionId, line, revision) => purchase
            ? InventoryClient.ShopBuyAsync(config, ApplicationAuthContext.Instance?.Token,
                transactionId, line.LootId.Value, line.Amount, revision)
            : InventoryClient.ShopSellAsync(config, ApplicationAuthContext.Instance?.Token,
                transactionId, line.LootId.Value, line.Amount, revision), reconciliationService.ReconcileAsync);
    }

    /// <summary>Explicit infrastructure injection, also used by deterministic tests.</summary>
    public void Initialize(LocalProfileStore store, BackendCall backend, Func<Task<bool>> reconcile)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _reconcile = reconcile ?? throw new ArgumentNullException(nameof(reconcile));
    }

    public async Task<StashOperationResult> ExecuteTradeAsync(ProfileId profileId, MerchantTradeTicket ticket)
    {
        if (_store == null || !_store.IsAvailable || _backend == null) return StashOperationResult.PersistenceFailed;
        if (!profileId.IsValid || _store.ProfileId != profileId || ticket == null)
            return StashOperationResult.InvalidInventory;
        if (ticket.LineCount != 1)
        {
            Debug.LogError($"[RemoteShopTransactionService] Trade {ticket.TransactionId.Value:N} has {ticket.LineCount} lines; the backend has no atomic multi-line trade endpoint.");
            return StashOperationResult.PersistenceFailed;
        }

        StashOperationResult localResult = _store.EvaluateTrade(profileId, ticket);
        if (localResult != StashOperationResult.Success) return localResult;

        bool purchase = ticket.Purchases.Count == 1;
        (bool success, ShopTransactionResult data, BackendError error) response;
        try
        {
            // Deliberately preserve Unity's synchronization context for all store/UI consumers.
            response = await _backend(purchase, ticket.TransactionId.Value.ToString("N"),
                purchase ? ticket.Purchases[0] : ticket.Sales[0], _store.RemoteRevision);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RemoteShopTransactionService] Backend outcome unknown: {exception.Message}");
            _ = ReconcileSafelyAsync();
            return StashOperationResult.PersistenceFailed;
        }

        if (response.success)
        {
            try
            {
                StashOperationResult committed = _store.TryCommitTrade(profileId, ticket);
                _store.SetRemoteRevision(response.data.revision);
                if ((committed != StashOperationResult.Success && committed != StashOperationResult.AlreadyApplied) ||
                    _store.RemoteRevision != response.data.revision)
                {
                    Debug.LogError($"[RemoteShopTransactionService] Backend persisted trade {ticket.TransactionId.Value:N} but local commit/revision save failed ({committed}); reconciling.");
                    _ = ReconcileSafelyAsync();
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[RemoteShopTransactionService] Backend persisted trade {ticket.TransactionId.Value:N} but local persistence threw: {exception.Message}");
                _ = ReconcileSafelyAsync();
            }
            // Includes alreadySecured: backend persistence is authoritative even if the cache failed.
            // Reporting failure here would incorrectly release stock for a durable transaction.
            return StashOperationResult.Success;
        }

        if (response.error.error == BackendErrorUtility.Unauthorized)
        {
            ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
            return StashOperationResult.PersistenceFailed;
        }

        _ = ReconcileSafelyAsync();
        return response.error.error switch
        {
            "RESERVATION_PENDING" => StashOperationResult.ReservationPending,
            "INSUFFICIENT_FUNDS" or "INSUFFICIENT_ITEMS" or "ITEM_NOT_SELLABLE" or "ITEM_NOT_OFFERED" or
                "REVISION_CONFLICT" or "VALIDATION_FAILED" => StashOperationResult.InvalidInventory,
            _ => StashOperationResult.PersistenceFailed
        };
    }

    private async Task ReconcileSafelyAsync()
    {
        try
        {
            if (!await _reconcile()) Debug.LogWarning("[RemoteShopTransactionService] Reconciliation failed.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RemoteShopTransactionService] Reconciliation failed: {exception.Message}");
        }
    }

    public bool IsTradeApplied(ProfileId profileId, ShopTransactionId transactionId) =>
        _store != null && _store.IsTradeApplied(profileId, transactionId);
}
