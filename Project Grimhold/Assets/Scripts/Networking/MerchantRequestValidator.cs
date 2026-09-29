using System;
using System.Collections.Generic;

/// <summary>
/// State Authority rules for merchant trades over the shared, replicated stock. A trade request is
/// identified by <c>ProfileId + RequestId</c>: an accepted request mints one
/// <see cref="ShopTransactionId"/>, reserves its purchase units and is answered with one priced,
/// multi-line <see cref="MerchantTradeTicket"/>. A reservation ends only with the requester's
/// outcome: persisted consumes it (and returns sold offered units to stock), not persisted
/// releases it. Time, departure or authority changes never release it, because a persisted
/// outcome may still arrive; unreported trades are resolved by the requester through
/// <see cref="GetPendingTrades"/>. Records stay after their outcome, bounded, so a late identical
/// retry gets the same ticket instead of becoming a new transaction. Every mutation is refused
/// when the backing state cannot be mutated, so proxies never change stock.
/// </summary>
public sealed class MerchantRequestValidator
{
    /// <summary>A trade never sells more distinct loot than the Inventory can hold.</summary>
    public const int MaxSaleLines = LocalProfileSnapshot.MaxLoadoutSlots;

    private static readonly IReadOnlyList<MerchantStockItem> EmptyStock = Array.Empty<MerchantStockItem>();

    private readonly IReadOnlyList<MerchantStockItem> _stock;
    private readonly IMerchantAuthorityState _state;
    private readonly Func<long> _timestampProvider;
    private readonly Func<Guid> _guidProvider;

    public MerchantRequestValidator(
        IReadOnlyList<MerchantStockItem> stock,
        IMerchantAuthorityState state,
        Func<long> timestampProvider = null,
        Func<Guid> guidProvider = null)
    {
        _stock = stock ?? EmptyStock;
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _timestampProvider = timestampProvider ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _guidProvider = guidProvider ?? Guid.NewGuid;
    }

    /// <summary>Most purchase lines one trade can hold: one per offered stock slot.</summary>
    public int MaxPurchaseLines => _state.StockSlotCapacity;

    public static bool TryValidateStockConfiguration(IReadOnlyList<MerchantStockItem> stock, int slotCapacity, out string error)
    {
        error = null;
        if (stock == null)
        {
            return true;
        }

        if (stock.Count > slotCapacity)
        {
            error = $"Merchant offers {stock.Count} items but supports at most {slotCapacity}.";
            return false;
        }

        for (int i = 0; i < stock.Count; i++)
        {
            MerchantStockItem item = stock[i];
            if (item.Item == null)
            {
                error = $"Merchant stock entry {i} has no item.";
                return false;
            }

            if (item.InitialQuantity < MerchantStockItem.UnlimitedQuantity)
            {
                error = $"Merchant stock entry '{item.Item.Id}' has an invalid initial quantity: {item.InitialQuantity}.";
                return false;
            }

            for (int previous = 0; previous < i; previous++)
            {
                if (stock[previous].Item != null && stock[previous].Item.Id == item.Item.Id)
                {
                    error = $"Merchant stock offers '{item.Item.Id}' more than once.";
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Seeds the shared stock from configuration. Only State Authority may do it.</summary>
    public bool TryInitializeStock()
    {
        if (!_state.CanMutate)
        {
            return false;
        }

        for (int slot = 0; slot < _state.StockSlotCapacity; slot++)
        {
            _state.SetStockQuantity(slot, slot < _stock.Count ? _stock[slot].InitialQuantity : 0);
        }

        _state.MarkStockChanged();
        return true;
    }

    /// <summary>
    /// Units that can still be purchased: unreserved stock, <see cref="MerchantStockItem.UnlimitedQuantity"/>
    /// for unlimited offerings, or zero when the item is not offered.
    /// </summary>
    public int GetAvailableStock(LootId lootId)
    {
        int slot = FindStockSlot(lootId);
        if (slot < 0)
        {
            return 0;
        }

        int quantity = _state.GetStockQuantity(slot);
        if (quantity == MerchantStockItem.UnlimitedQuantity)
        {
            return MerchantStockItem.UnlimitedQuantity;
        }

        return Math.Max(0, quantity - GetReservedQuantity(slot));
    }

    /// <summary>
    /// Evaluates one trade request. Returns false when no response must be sent (not State
    /// Authority). A known <c>ProfileId + RequestId</c> with the same lines is answered with its
    /// original ticket: pending or committed trades are not reserved again, released ones reserve
    /// again under the same transaction id. The same key with different lines is rejected.
    /// </summary>
    public bool TryProcessTradeRequest(
        ProfileId profileId,
        Guid requestId,
        IReadOnlyList<MerchantTradeLine> purchases,
        IReadOnlyList<MerchantTradeLine> sales,
        LootDefinitionCatalog catalog,
        out MerchantTradeResponse response)
    {
        response = MerchantTradeResponse.Rejected(requestId);
        if (!_state.CanMutate)
        {
            return false;
        }

        if (!profileId.IsValid || requestId == Guid.Empty || purchases == null || sales == null ||
            purchases.Count + sales.Count == 0)
        {
            return true;
        }

        var pricedPurchases = new List<MerchantPricedTradeLine>(purchases.Count);
        var pricedSales = new List<MerchantPricedTradeLine>(sales.Count);
        if (!TryPriceLines(purchases, isPurchase: true, catalog, pricedPurchases) ||
            !TryPriceLines(sales, isPurchase: false, catalog, pricedSales))
        {
            return true;
        }

        long profileKey = MerchantTradeRecord.HashProfile(profileId);
        long payloadHash = MerchantTradeRecord.HashPayload(purchases, sales);
        int recordIndex = FindRecord(profileKey, requestId);
        ShopTransactionId transactionId = default;
        if (recordIndex >= 0)
        {
            MerchantTradeRecord existing = _state.GetTradeRecord(recordIndex);
            if (existing.PayloadHash != payloadHash)
            {
                return true;
            }

            transactionId = existing.TransactionId;
            if (existing.RecordStatus != MerchantTradeRecordStatus.Released)
            {
                response = MerchantTradeResponse.Approved(requestId, new MerchantTradeTicket(transactionId, pricedPurchases, pricedSales));
                return true;
            }
        }

        var effects = new List<MerchantTradeStockLine>();
        if (!TryPlanStockEffects(purchases, sales, effects) || CountFreeStockLines() < effects.Count)
        {
            return true;
        }

        if (recordIndex < 0)
        {
            recordIndex = FindRecordSlot();
            if (recordIndex < 0)
            {
                return true;
            }

            transactionId = new ShopTransactionId(_timestampProvider(), _guidProvider());
        }

        _state.SetTradeRecord(recordIndex, MerchantTradeRecord.CreatePending(profileKey, requestId, payloadHash, transactionId));

        bool reservesStock = false;
        foreach (MerchantTradeStockLine effect in effects)
        {
            _state.SetTradeStockLine(FindFreeStockLine(), MerchantTradeStockLine.Create(recordIndex, effect.StockSlot, effect.Amount, effect.IsPurchase));
            reservesStock |= effect.IsPurchase;
        }

        if (reservesStock)
        {
            _state.MarkStockChanged();
        }

        response = MerchantTradeResponse.Approved(requestId, new MerchantTradeTicket(transactionId, pricedPurchases, pricedSales));
        return true;
    }

    /// <summary>
    /// Applies the requester's persistence outcome to its pending trade: persisted consumes the
    /// reservations and returns sold offered units to stock, not persisted only releases them.
    /// Outcomes for trades that are not pending, other transaction ids or unknown keys are ignored:
    /// a released ticket is never persisted without being reserved again first.
    /// </summary>
    public bool TryCompleteTrade(ProfileId profileId, Guid requestId, ShopTransactionId transactionId, bool persisted)
    {
        if (!_state.CanMutate || !profileId.IsValid)
        {
            return false;
        }

        int index = FindRecord(MerchantTradeRecord.HashProfile(profileId), requestId);
        if (index < 0)
        {
            return false;
        }

        MerchantTradeRecord record = _state.GetTradeRecord(index);
        if (!record.IsPending || !record.TransactionId.Equals(transactionId))
        {
            return false;
        }

        ReleaseStockLines(index, applyCommittedChanges: persisted);
        _state.SetTradeRecord(index, record.WithStatus(persisted ? MerchantTradeRecordStatus.Committed : MerchantTradeRecordStatus.Released));
        return true;
    }

    /// <summary>
    /// Collects the pending trades of one profile so its requester can report their real outcome
    /// from the confirmed profile, e.g. after a lost report, a reconnection or an authority change.
    /// </summary>
    public int GetPendingTrades(ProfileId profileId, List<MerchantTradeRecord> pending)
    {
        if (pending == null)
        {
            throw new ArgumentNullException(nameof(pending));
        }

        pending.Clear();
        if (!profileId.IsValid)
        {
            return 0;
        }

        long profileKey = MerchantTradeRecord.HashProfile(profileId);
        for (int index = 0; index < _state.TradeRecordCapacity; index++)
        {
            MerchantTradeRecord record = _state.GetTradeRecord(index);
            if (record.IsPendingFor(profileKey))
            {
                pending.Add(record);
            }
        }

        return pending.Count;
    }

    private bool TryPriceLines(
        IReadOnlyList<MerchantTradeLine> lines,
        bool isPurchase,
        LootDefinitionCatalog catalog,
        List<MerchantPricedTradeLine> priced)
    {
        if (catalog == null || lines.Count > (isPurchase ? MaxPurchaseLines : MaxSaleLines))
        {
            return false;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            MerchantTradeLine line = lines[i];
            if (!line.LootId.IsValid || line.Amount <= 0 ||
                !catalog.TryGet(line.LootId.Value, out LootDefinition definition) || definition == null)
            {
                return false;
            }

            for (int previous = 0; previous < i; previous++)
            {
                if (lines[previous].LootId == line.LootId)
                {
                    return false;
                }
            }

            int unitPrice = isPurchase ? definition.BuyValuePerUnit : definition.SellValuePerUnit;
            if (isPurchase ? unitPrice <= 0 || FindStockSlot(line.LootId) < 0 : unitPrice < 0)
            {
                return false;
            }

            priced.Add(new MerchantPricedTradeLine(line.LootId, line.Amount, unitPrice));
        }

        return true;
    }

    private bool TryPlanStockEffects(
        IReadOnlyList<MerchantTradeLine> purchases,
        IReadOnlyList<MerchantTradeLine> sales,
        List<MerchantTradeStockLine> effects)
    {
        foreach (MerchantTradeLine purchase in purchases)
        {
            int slot = FindStockSlot(purchase.LootId);
            if (!HasAvailableStock(slot, purchase.Amount))
            {
                return false;
            }

            if (_state.GetStockQuantity(slot) != MerchantStockItem.UnlimitedQuantity)
            {
                effects.Add(MerchantTradeStockLine.Create(-1, slot, purchase.Amount, isPurchase: true));
            }
        }

        foreach (MerchantTradeLine sale in sales)
        {
            int slot = FindStockSlot(sale.LootId);
            if (slot >= 0 && _state.GetStockQuantity(slot) != MerchantStockItem.UnlimitedQuantity)
            {
                effects.Add(MerchantTradeStockLine.Create(-1, slot, sale.Amount, isPurchase: false));
            }
        }

        return true;
    }

    private void ReleaseStockLines(int recordIndex, bool applyCommittedChanges)
    {
        bool stockChanged = false;
        for (int index = 0; index < _state.TradeStockLineCapacity; index++)
        {
            MerchantTradeStockLine line = _state.GetTradeStockLine(index);
            if (!line.IsActive || line.RecordIndex != recordIndex)
            {
                continue;
            }

            if (applyCommittedChanges)
            {
                ApplyCommittedStockChange(line);
                stockChanged = true;
            }

            stockChanged |= line.IsPurchase;
            _state.SetTradeStockLine(index, default);
        }

        if (stockChanged)
        {
            _state.MarkStockChanged();
        }
    }

    private void ApplyCommittedStockChange(MerchantTradeStockLine line)
    {
        int quantity = _state.GetStockQuantity(line.StockSlot);
        if (quantity == MerchantStockItem.UnlimitedQuantity)
        {
            return;
        }

        int next = line.IsPurchase
            ? Math.Max(0, quantity - line.Amount)
            : (quantity > int.MaxValue - line.Amount ? int.MaxValue : quantity + line.Amount);

        _state.SetStockQuantity(line.StockSlot, next);
    }

    private bool HasAvailableStock(int slot, int amount)
    {
        if (slot < 0)
        {
            return false;
        }

        int quantity = _state.GetStockQuantity(slot);
        if (quantity == MerchantStockItem.UnlimitedQuantity)
        {
            return true;
        }

        return quantity - GetReservedQuantity(slot) >= amount;
    }

    private int GetReservedQuantity(int slot)
    {
        int reserved = 0;
        for (int index = 0; index < _state.TradeStockLineCapacity; index++)
        {
            MerchantTradeStockLine line = _state.GetTradeStockLine(index);
            if (line.Reserves(slot))
            {
                reserved += line.Amount;
            }
        }

        return reserved;
    }

    private int FindStockSlot(LootId lootId)
    {
        if (!lootId.IsValid)
        {
            return -1;
        }

        int count = Math.Min(_stock.Count, _state.StockSlotCapacity);
        for (int slot = 0; slot < count; slot++)
        {
            if (_stock[slot].Item != null && _stock[slot].Item.Id == lootId.Value)
            {
                return slot;
            }
        }

        return -1;
    }

    private int FindRecord(long profileKey, Guid requestId)
    {
        for (int index = 0; index < _state.TradeRecordCapacity; index++)
        {
            if (_state.GetTradeRecord(index).Matches(profileKey, requestId))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// A free record, or else the oldest completed one: completed records are only deduplication
    /// history and are evicted oldest first. Pending records are never evicted.
    /// </summary>
    private int FindRecordSlot()
    {
        int oldest = -1;
        long oldestTimestamp = long.MaxValue;
        for (int index = 0; index < _state.TradeRecordCapacity; index++)
        {
            MerchantTradeRecord record = _state.GetTradeRecord(index);
            if (!record.IsOccupied)
            {
                return index;
            }

            if (!record.IsPending && record.TransactionTimestamp < oldestTimestamp)
            {
                oldest = index;
                oldestTimestamp = record.TransactionTimestamp;
            }
        }

        return oldest;
    }

    private int CountFreeStockLines()
    {
        int free = 0;
        for (int index = 0; index < _state.TradeStockLineCapacity; index++)
        {
            if (!_state.GetTradeStockLine(index).IsActive)
            {
                free++;
            }
        }

        return free;
    }

    private int FindFreeStockLine()
    {
        for (int index = 0; index < _state.TradeStockLineCapacity; index++)
        {
            if (!_state.GetTradeStockLine(index).IsActive)
            {
                return index;
            }
        }

        return -1;
    }
}
