using System;
using System.Collections.Generic;

/// <summary>
/// One merchant or Inventory row, already priced for display. <see cref="Available"/> is the
/// unreserved stock (<see cref="MerchantStockItem.UnlimitedQuantity"/> when unlimited) for a
/// merchant row and the confirmed owned units for an Inventory row.
/// </summary>
public readonly struct MerchantShopRowViewModel
{
    /// <summary>Largest draft amount offered, and displayed, for an unlimited offering.</summary>
    public const int UnlimitedDraftAmount = 999;

    public LootDefinition Definition { get; }
    public LootId LootId { get; }
    public bool IsMerchantStock { get; }
    public int UnitPrice { get; }
    public int Available { get; }
    public int DraftAmount { get; }
    public long DraftTotal { get; }
    public bool CanAdd { get; }

    /// <summary>Largest amount the draft line may be set to; at least the current draft amount.</summary>
    public int MaxDraftAmount { get; }

    public bool IsUnlimited => Available == MerchantStockItem.UnlimitedQuantity;

    public MerchantShopRowViewModel(
        LootDefinition definition,
        LootId lootId,
        bool isMerchantStock,
        int unitPrice,
        int available,
        int draftAmount,
        long draftTotal,
        bool canAdd,
        int maxDraftAmount)
    {
        Definition = definition;
        LootId = lootId;
        IsMerchantStock = isMerchantStock;
        UnitPrice = unitPrice;
        Available = available;
        DraftAmount = draftAmount;
        DraftTotal = draftTotal;
        CanAdd = canAdd;
        MaxDraftAmount = maxDraftAmount;
    }
}

/// <summary>
/// Everything <see cref="MerchantShopUI"/> renders for one merchant session: confirmed Gold, the
/// preview of the pending trade and the rows. The UI never computes prices, totals, capacity or
/// whether a trade may be confirmed; it reads them from here.
/// </summary>
public sealed class MerchantShopViewModel
{
    public bool IsAvailable { get; }
    public long ConfirmedCurrency { get; }
    public long ProjectedCurrency { get; }
    public long PurchaseTotal { get; }
    public long SaleTotal { get; }
    public long Balance { get; }

    /// <summary>Gold missing for the projected Currency to reach zero; zero when affordable.</summary>
    public long CurrencyShortfall => ProjectedCurrency < 0 ? -ProjectedCurrency : 0;

    public int OccupiedSlots { get; }
    public int ProjectedOccupiedSlots { get; }
    public int SlotCapacity { get; }
    public bool HasDraft { get; }
    public bool IsInFlight { get; }
    public bool CanEdit { get; }
    public bool CanConfirm { get; }
    public bool CanClear { get; }
    public IReadOnlyList<MerchantTradeBlock> Blocks { get; }
    public IReadOnlyList<MerchantShopRowViewModel> MerchantRows { get; }
    public IReadOnlyList<MerchantShopRowViewModel> InventoryRows { get; }

    public static readonly MerchantShopViewModel Unavailable = new MerchantShopViewModel(
        false, 0, 0, 0, 0, 0, 0, 0, 0, false, false, false,
        Array.Empty<MerchantTradeBlock>(), Array.Empty<MerchantShopRowViewModel>(), Array.Empty<MerchantShopRowViewModel>());

    public MerchantShopViewModel(
        bool isAvailable,
        long confirmedCurrency,
        long projectedCurrency,
        long purchaseTotal,
        long saleTotal,
        long balance,
        int occupiedSlots,
        int projectedOccupiedSlots,
        int slotCapacity,
        bool hasDraft,
        bool isInFlight,
        bool canConfirm,
        IReadOnlyList<MerchantTradeBlock> blocks,
        IReadOnlyList<MerchantShopRowViewModel> merchantRows,
        IReadOnlyList<MerchantShopRowViewModel> inventoryRows)
    {
        IsAvailable = isAvailable;
        ConfirmedCurrency = confirmedCurrency;
        ProjectedCurrency = projectedCurrency;
        PurchaseTotal = purchaseTotal;
        SaleTotal = saleTotal;
        Balance = balance;
        OccupiedSlots = occupiedSlots;
        ProjectedOccupiedSlots = projectedOccupiedSlots;
        SlotCapacity = slotCapacity;
        HasDraft = hasDraft;
        IsInFlight = isInFlight;
        CanEdit = isAvailable && !isInFlight;
        CanConfirm = isAvailable && canConfirm;
        CanClear = CanEdit && hasDraft;
        Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
        MerchantRows = merchantRows ?? throw new ArgumentNullException(nameof(merchantRows));
        InventoryRows = inventoryRows ?? throw new ArgumentNullException(nameof(inventoryRows));
    }
}
