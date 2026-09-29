using System;
using System.Collections.Generic;

/// <summary>
/// Pure C# state of one open merchant session for the local Input Authority player, owned by
/// <see cref="TownMerchantPresenter"/>. It holds the <see cref="MerchantTradeDraft"/>, recomputes
/// the <see cref="MerchantTradePreview"/> from the confirmed profile and replicated stock, sends a
/// confirmation as one multi-line request, persists the authority-issued ticket through
/// <see cref="IShopTransactionService"/> and reports the outcome back to State Authority.
/// Confirmed Inventory and Currency are only re-read when the profile commits or stock changes.
/// It never persists a ticket outside its in-flight request, so a not-persisted report is final;
/// pending trades it no longer uses are resolved from the confirmed profile's receipts.
/// </summary>
public sealed class TownMerchantTradeSession : IMerchantShopIntentions, IMerchantTradeClient, IDisposable
{
    private readonly ProfileId _profileId;
    private readonly IMerchantTradeEndpoint _endpoint;
    private readonly IShopTransactionService _shopService;
    private readonly IInventoryReadSource _inventory;
    private readonly Func<long> _readCurrency;
    private readonly Action<MerchantShopViewModel> _present;
    private readonly Func<double> _clock;
    private readonly double _submissionTimeoutSeconds;
    private readonly MerchantTradeDraft _draft;
    private Guid? _unansweredRequestId;
    private double _submittedAt;
    private bool _disposed;

    public event Action<MerchantTransactionResult> TradeCompleted;

    public MerchantShopViewModel Current { get; private set; } = MerchantShopViewModel.Unavailable;
    public bool IsSubmissionInFlight => _draft.IsInFlight;

    public TownMerchantTradeSession(
        ProfileId profileId,
        IMerchantTradeEndpoint endpoint,
        IShopTransactionService shopService,
        IInventoryReadSource inventory,
        Func<long> readCurrency,
        Action<MerchantShopViewModel> present,
        Func<double> clock,
        double submissionTimeoutSeconds,
        Func<Guid> requestIdProvider = null)
    {
        if (!profileId.IsValid)
        {
            throw new ArgumentException("The merchant profile must be valid.", nameof(profileId));
        }

        if (submissionTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(submissionTimeoutSeconds));
        }

        _profileId = profileId;
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _shopService = shopService ?? throw new ArgumentNullException(nameof(shopService));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _readCurrency = readCurrency ?? throw new ArgumentNullException(nameof(readCurrency));
        _present = present ?? throw new ArgumentNullException(nameof(present));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _submissionTimeoutSeconds = submissionTimeoutSeconds;
        _draft = new MerchantTradeDraft(requestIdProvider);

        _endpoint.StockChanged += Refresh;
        _endpoint.AuthorityChanged += OnAuthorityChanged;
        _inventory.Changed += Refresh;
        _endpoint.SetTradeClient(this);
        Refresh();

        // Trades left unreported by an earlier session keep their stock reserved until resolved.
        _endpoint.RequestPendingTrades(_profileId);
    }

    public bool AddPurchase(LootId lootId, int amount) => Edit(_draft.TryAddPurchase(lootId, amount));
    public bool SetPurchaseAmount(LootId lootId, int amount) => Edit(_draft.TrySetPurchaseAmount(lootId, amount));
    public bool RemovePurchase(LootId lootId) => Edit(_draft.TryRemovePurchase(lootId));

    public bool AddSale(LootId lootId, int amount) => Edit(_draft.TryAddSale(lootId, amount));
    public bool SetSaleAmount(LootId lootId, int amount) => Edit(_draft.TrySetSaleAmount(lootId, amount));
    public bool RemoveSale(LootId lootId) => Edit(_draft.TryRemoveSale(lootId));

    public bool ClearTrade() => Edit(_draft.TryClear());

    /// <summary>
    /// Sends the whole draft as one request, only when the current preview allows it. The draft
    /// stays locked until State Authority answers.
    /// </summary>
    public bool ConfirmTrade()
    {
        if (_disposed || !TryCalculatePreview(out MerchantTradePreview preview, out _, out _) || !preview.CanConfirm ||
            !_draft.TryBeginSubmission(out Guid requestId))
        {
            return false;
        }

        if (!_endpoint.TrySubmitTrade(_profileId, requestId, _draft.Purchases, _draft.Sales))
        {
            _draft.MarkSubmissionRejectedOrFailed();
            Complete(MerchantTransactionResult.SubmissionFailed);
            return false;
        }

        _unansweredRequestId = requestId;
        _submittedAt = _clock();
        Refresh();
        return true;
    }

    /// <summary>
    /// Unlocks a submission that got no answer in time; an unchanged retry reuses its request id.
    /// Presentation only: a reservation accepted meanwhile stays held until its outcome is known.
    /// </summary>
    public void Tick()
    {
        if (!_disposed && _draft.IsInFlight && _clock() - _submittedAt >= _submissionTimeoutSeconds)
        {
            AbandonSubmission();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _endpoint.StockChanged -= Refresh;
        _endpoint.AuthorityChanged -= OnAuthorityChanged;
        _inventory.Changed -= Refresh;
        _endpoint.ClearTradeClient(this);
        TradeCompleted = null;
    }

    public bool HandleTradeResponse(MerchantTradeResponse response)
    {
        if (_disposed || !_draft.IsInFlight || _draft.RequestId != response.RequestId)
        {
            return false;
        }

        _unansweredRequestId = null;

        if (!response.IsApproved)
        {
            _draft.MarkSubmissionRejectedOrFailed();
            Complete(MerchantTransactionResult.RejectedByMerchant);
            return true;
        }

        StashOperationResult result = _shopService.TryExecuteTrade(_profileId, response.Ticket);
        bool persisted = result == StashOperationResult.Success || result == StashOperationResult.AlreadyApplied;
        _endpoint.ReportTradeOutcome(response.RequestId, response.Ticket.TransactionId, persisted);

        if (persisted)
        {
            _draft.MarkSubmissionSucceeded();
        }
        else
        {
            _draft.MarkSubmissionRejectedOrFailed();
        }

        Complete(MapPersistenceResult(result));
        return true;
    }

    /// <summary>
    /// Reports the real outcome of a pending trade this session no longer uses: persisted when the
    /// confirmed profile holds its receipt, otherwise not persisted, which is final because only
    /// the in-flight request is ever persisted. The current request is left alone.
    /// </summary>
    public void HandlePendingTrade(Guid requestId, ShopTransactionId transactionId)
    {
        if (_disposed || _draft.RequestId == requestId)
        {
            return;
        }

        _endpoint.ReportTradeOutcome(requestId, transactionId, _shopService.IsTradeApplied(_profileId, transactionId));
    }

    private void OnAuthorityChanged()
    {
        if (_draft.IsInFlight)
        {
            AbandonSubmission();
        }

        // Reports sent to the previous authority may be lost; the replicated records remain.
        _endpoint.RequestPendingTrades(_profileId);
    }

    private void AbandonSubmission()
    {
        _draft.MarkSubmissionRejectedOrFailed();
        Complete(MerchantTransactionResult.NoResponse);
    }

    private void Complete(MerchantTransactionResult result)
    {
        Refresh();
        TradeCompleted?.Invoke(result);
    }

    private bool Edit(bool changed)
    {
        if (_disposed || !changed)
        {
            return changed;
        }

        if (_unansweredRequestId.HasValue && _draft.RequestId != _unansweredRequestId)
        {
            // The edit discarded a sent request, so it can no longer be persisted: resolve it now
            // instead of holding its reservation until the next session.
            _unansweredRequestId = null;
            _endpoint.RequestPendingTrades(_profileId);
        }

        Refresh();
        return changed;
    }

    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        Current = BuildViewModel();
        _present(Current);
    }

    private bool TryCalculatePreview(out MerchantTradePreview preview, out IReadOnlyList<LootEntry> inventory, out long currency)
    {
        preview = null;
        currency = 0;
        if (_endpoint.Catalog == null || !_inventory.TryGetLootContent(out inventory))
        {
            inventory = null;
            return false;
        }

        currency = _readCurrency();
        if (currency < 0)
        {
            return false;
        }

        try
        {
            preview = MerchantTradePreview.Calculate(
                inventory, _inventory.SlotCapacity, currency, _draft, _endpoint.GetAvailableStock, _endpoint.Catalog);
            return true;
        }
        catch (ArgumentException)
        {
            // An invalid confirmed Inventory never allows a confirmation.
            return false;
        }
    }

    private MerchantShopViewModel BuildViewModel()
    {
        if (!TryCalculatePreview(out MerchantTradePreview preview, out IReadOnlyList<LootEntry> inventory, out long currency))
        {
            return MerchantShopViewModel.Unavailable;
        }

        bool canEdit = !_draft.IsInFlight;
        return new MerchantShopViewModel(
            isAvailable: true,
            currency,
            preview.ProjectedCurrency,
            preview.PurchaseTotal,
            preview.SaleTotal,
            preview.Balance,
            preview.ConfirmedOccupiedSlots,
            preview.ProjectedOccupiedSlots,
            preview.SlotCapacity,
            hasDraft: !_draft.IsEmpty,
            _draft.IsInFlight,
            preview.CanConfirm,
            preview.Blocks,
            BuildMerchantRows(canEdit),
            BuildInventoryRows(inventory, canEdit));
    }

    private IReadOnlyList<MerchantShopRowViewModel> BuildMerchantRows(bool canEdit)
    {
        var rows = new List<MerchantShopRowViewModel>();
        IReadOnlyList<MerchantStockItem> stock = _endpoint.Stock;
        for (int i = 0; stock != null && i < stock.Count; i++)
        {
            LootDefinition definition = stock[i].Item;
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                continue;
            }

            var lootId = new LootId(definition.Id);
            int available = _endpoint.GetAvailableStock(lootId);
            int draftAmount = FindAmount(_draft.Purchases, lootId);
            int limit = available == MerchantStockItem.UnlimitedQuantity ? MerchantShopRowViewModel.UnlimitedDraftAmount : available;
            rows.Add(new MerchantShopRowViewModel(
                definition,
                lootId,
                isMerchantStock: true,
                definition.BuyValuePerUnit,
                available,
                draftAmount,
                (long)definition.BuyValuePerUnit * draftAmount,
                canAdd: canEdit && definition.BuyValuePerUnit > 0 && draftAmount < limit,
                maxDraftAmount: Math.Max(limit, draftAmount)));
        }

        return rows;
    }

    private IReadOnlyList<MerchantShopRowViewModel> BuildInventoryRows(IReadOnlyList<LootEntry> inventory, bool canEdit)
    {
        var rows = new List<MerchantShopRowViewModel>();
        foreach (LootEntry entry in inventory)
        {
            AddInventoryRow(rows, entry.LootId, entry.Amount, canEdit);
        }

        // Sale lines whose units are no longer owned stay visible so they can be removed.
        foreach (MerchantTradeLine sale in _draft.Sales)
        {
            if (FindRow(rows, sale.LootId) < 0)
            {
                AddInventoryRow(rows, sale.LootId, owned: 0, canEdit);
            }
        }

        return rows;
    }

    private void AddInventoryRow(List<MerchantShopRowViewModel> rows, LootId lootId, int owned, bool canEdit)
    {
        if (!_endpoint.Catalog.TryGet(lootId.Value, out LootDefinition definition) || definition == null)
        {
            return;
        }

        int draftAmount = FindAmount(_draft.Sales, lootId);
        rows.Add(new MerchantShopRowViewModel(
            definition,
            lootId,
            isMerchantStock: false,
            definition.SellValuePerUnit,
            owned,
            draftAmount,
            (long)definition.SellValuePerUnit * draftAmount,
            canAdd: canEdit && draftAmount < owned,
            maxDraftAmount: Math.Max(owned, draftAmount)));
    }

    private static int FindRow(List<MerchantShopRowViewModel> rows, LootId lootId)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].LootId == lootId)
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindAmount(IReadOnlyList<MerchantTradeLine> lines, LootId lootId)
    {
        foreach (MerchantTradeLine line in lines)
        {
            if (line.LootId == lootId)
            {
                return line.Amount;
            }
        }

        return 0;
    }

    private static MerchantTransactionResult MapPersistenceResult(StashOperationResult result)
    {
        return result switch
        {
            StashOperationResult.Success => MerchantTransactionResult.Success,
            StashOperationResult.AlreadyApplied => MerchantTransactionResult.AlreadyApplied,
            StashOperationResult.PersistenceFailed => MerchantTransactionResult.PersistenceFailed,
            _ => MerchantTransactionResult.RejectedByProfile
        };
    }
}
