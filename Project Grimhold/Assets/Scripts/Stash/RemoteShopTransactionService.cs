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
public sealed class RemoteShopTransactionService : MonoBehaviour, IShopTransactionService, IBuybackService
{
    // One infrastructure seam for the two single-line endpoints; no separate commerce transport.
    public delegate Task<(bool success, ShopTransactionResult data, BackendError error)> BackendCall(
        bool purchase, string transactionId, MerchantPricedTradeLine line, int expectedRevision);

    public delegate Task<(bool success, ShopTransactionResult data, BackendError error)> BuybackBackendCall(
        string transactionId, string entryId, int amount, int expectedRevision);

    private BuybackBackendCall _buybackBackend;
    private BuybackLedger _buybackLedger;
    private LocalProfileStore _store;
    private BackendCall _backend;
    private Func<Task<bool>> _reconcile;

    public void Initialize(LocalProfileStore store, LocalProfilePersistenceConfiguration localConfig,
        ProfileReconciliationService reconciliationService, BuybackLedger ledger = null)
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
        if (ledger != null)
            InitializeBuyback(ledger, (transactionId, entryId, amount, revision) =>
                InventoryClient.ShopBuybackAsync(config, ApplicationAuthContext.Instance?.Token,
                    transactionId, entryId, amount, revision));
    }

    /// <summary>Explicit infrastructure injection, also used by deterministic tests.</summary>
    public void Initialize(LocalProfileStore store, BackendCall backend, Func<Task<bool>> reconcile)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _reconcile = reconcile ?? throw new ArgumentNullException(nameof(reconcile));
    }

    public void InitializeBuyback(BuybackLedger ledger, BuybackBackendCall backend)
    {
        _buybackLedger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _buybackBackend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public async Task<StashOperationResult> ExecuteBuybackAsync(ProfileId profileId, MerchantBuybackRequest request)
    {
        if (_store == null || !_store.IsAvailable || _buybackBackend == null) return StashOperationResult.PersistenceFailed;
        if (string.IsNullOrWhiteSpace(request.EntryId) || request.UnitPrice < 0 || request.Amount < 1 ||
            (request.UnitPrice > 0 && request.Amount > long.MaxValue / request.UnitPrice))
            return StashOperationResult.InvalidInventory;
        var localResult = _store.EvaluateBuyback(profileId, request.TransactionId, request.LootId,
            request.Amount, request.Amount * request.UnitPrice);
        if (localResult != StashOperationResult.Success) return localResult;

        (bool success, ShopTransactionResult data, BackendError error) response;
        try
        {
            response = await _buybackBackend(request.TransactionId.Value.ToString("N"), request.EntryId,
                request.Amount, _store.RemoteRevision);
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
                var committed = _store.ApplyBuybackAcknowledgement(profileId, request.TransactionId, response.data);
                _buybackLedger.Replace(response.data.buyback);
                if (committed != StashOperationResult.Success && committed != StashOperationResult.AlreadyApplied)
                {
                    Debug.LogError($"[RemoteShopTransactionService] Backend persisted buyback but local commit failed ({committed}); reconciling.");
                    _ = ReconcileSafelyAsync();
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[RemoteShopTransactionService] Backend persisted buyback but local persistence threw: {exception.Message}");
                _ = ReconcileSafelyAsync();
            }
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
            "BUYBACK_ENTRY_NOT_FOUND" or "INSUFFICIENT_BUYBACK_UNITS" or "INSUFFICIENT_FUNDS" or
                "REVISION_CONFLICT" or "VALIDATION_FAILED" => StashOperationResult.InvalidInventory,
            _ => StashOperationResult.PersistenceFailed
        };
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
                _buybackLedger?.Replace(response.data.buyback);
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
