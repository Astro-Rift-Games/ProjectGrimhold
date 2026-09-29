using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// Local pending state of one merchant trade: ordered buy and sell lines plus the request
/// identity of its current submission. It never reads or mutates Inventory, Currency, stock,
/// persistence or networked state, and it computes no prices, balance or capacity.
/// </summary>
public sealed class MerchantTradeDraft
{
    private readonly List<MerchantTradeLine> _purchases = new List<MerchantTradeLine>();
    private readonly List<MerchantTradeLine> _sales = new List<MerchantTradeLine>();
    private readonly ReadOnlyCollection<MerchantTradeLine> _purchasesView;
    private readonly ReadOnlyCollection<MerchantTradeLine> _salesView;
    private readonly Func<Guid> _requestIdProvider;

    public IReadOnlyList<MerchantTradeLine> Purchases => _purchasesView;
    public IReadOnlyList<MerchantTradeLine> Sales => _salesView;

    public bool IsEmpty => _purchases.Count == 0 && _sales.Count == 0;
    public bool IsInFlight { get; private set; }

    /// <summary>
    /// Identity of the current submission. It survives a rejected or failed submission so an
    /// unchanged retry reuses it, and is discarded by any effective edit.
    /// </summary>
    public Guid? RequestId { get; private set; }

    public MerchantTradeDraft(Func<Guid> requestIdProvider = null)
    {
        _requestIdProvider = requestIdProvider ?? Guid.NewGuid;
        _purchasesView = _purchases.AsReadOnly();
        _salesView = _sales.AsReadOnly();
    }

    public bool TryAddPurchase(LootId lootId, int amount) => TryAdd(_purchases, lootId, amount);
    public bool TrySetPurchaseAmount(LootId lootId, int amount) => TrySetAmount(_purchases, lootId, amount);
    public bool TryRemovePurchase(LootId lootId) => TryRemove(_purchases, lootId);

    public bool TryAddSale(LootId lootId, int amount) => TryAdd(_sales, lootId, amount);
    public bool TrySetSaleAmount(LootId lootId, int amount) => TrySetAmount(_sales, lootId, amount);
    public bool TryRemoveSale(LootId lootId) => TryRemove(_sales, lootId);

    /// <summary>
    /// Discards every line and the request identity. Rejected while a submission is in flight.
    /// </summary>
    public bool TryClear()
    {
        if (IsInFlight)
        {
            return false;
        }

        _purchases.Clear();
        _sales.Clear();
        RequestId = null;
        return true;
    }

    /// <summary>
    /// Starts a submission of the current content. Reuses the existing identity for an
    /// unchanged retry and creates one otherwise.
    /// </summary>
    public bool TryBeginSubmission(out Guid requestId)
    {
        if (IsInFlight || IsEmpty)
        {
            requestId = default;
            return false;
        }

        if (!RequestId.HasValue)
        {
            RequestId = _requestIdProvider();
        }

        requestId = RequestId.Value;
        IsInFlight = true;
        return true;
    }

    /// <summary>
    /// Releases the in-flight state and keeps lines and identity for an unchanged retry.
    /// </summary>
    public void MarkSubmissionRejectedOrFailed()
    {
        IsInFlight = false;
    }

    /// <summary>
    /// Completes the in-flight submission and resets the draft. Ignored when nothing is in flight.
    /// </summary>
    public void MarkSubmissionSucceeded()
    {
        if (!IsInFlight)
        {
            return;
        }

        _purchases.Clear();
        _sales.Clear();
        RequestId = null;
        IsInFlight = false;
    }

    private bool TryAdd(List<MerchantTradeLine> lines, LootId lootId, int amount)
    {
        if (!CanEdit(lootId, amount))
        {
            return false;
        }

        int index = IndexOf(lines, lootId);
        if (index < 0)
        {
            lines.Add(new MerchantTradeLine(lootId, amount));
            RequestId = null;
            return true;
        }

        int current = lines[index].Amount;
        if (current > int.MaxValue - amount)
        {
            return false;
        }

        lines[index] = new MerchantTradeLine(lootId, current + amount);
        RequestId = null;
        return true;
    }

    private bool TrySetAmount(List<MerchantTradeLine> lines, LootId lootId, int amount)
    {
        if (!CanEdit(lootId, amount))
        {
            return false;
        }

        int index = IndexOf(lines, lootId);
        if (index < 0)
        {
            return false;
        }

        if (lines[index].Amount != amount)
        {
            lines[index] = new MerchantTradeLine(lootId, amount);
            RequestId = null;
        }

        return true;
    }

    private bool TryRemove(List<MerchantTradeLine> lines, LootId lootId)
    {
        if (IsInFlight || !lootId.IsValid)
        {
            return false;
        }

        int index = IndexOf(lines, lootId);
        if (index < 0)
        {
            return false;
        }

        lines.RemoveAt(index);
        RequestId = null;
        return true;
    }

    private bool CanEdit(LootId lootId, int amount) => !IsInFlight && lootId.IsValid && amount > 0;

    private static int IndexOf(List<MerchantTradeLine> lines, LootId lootId)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].LootId == lootId)
            {
                return i;
            }
        }

        return -1;
    }
}
