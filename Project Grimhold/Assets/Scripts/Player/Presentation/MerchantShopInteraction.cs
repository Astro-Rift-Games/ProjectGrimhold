using System;
using System.Collections.Generic;

/// <summary>What the center action of the merchant shop does with the selected row.</summary>
public enum MerchantShopLineAction
{
    None,
    Add,
    Update
}

/// <summary>
/// Presentation state of <see cref="MerchantShopUI"/>: the selected row, the quantity being chosen
/// for it and the single edit flow (select, choose quantity, add or update the line, review,
/// confirm or clear). It reads limits and permissions from the <see cref="MerchantShopViewModel"/>
/// and raises <see cref="IMerchantShopIntentions"/>; it computes no price, total or trade rule.
/// </summary>
public sealed class MerchantShopInteraction
{
    private readonly IMerchantShopIntentions _intentions;
    private LootId _selectedLootId;
    private bool _isSelectedFromMerchant;
    private int _syncedDraftAmount;
    private bool _confirmPending;

    public MerchantShopViewModel ViewModel { get; private set; } = MerchantShopViewModel.Unavailable;
    public bool HasSelection { get; private set; }
    public MerchantShopRowViewModel SelectedRow { get; private set; }

    /// <summary>Quantity the center action applies to the selected line.</summary>
    public int Quantity { get; private set; }

    public MerchantShopInteraction(IMerchantShopIntentions intentions)
    {
        _intentions = intentions ?? throw new ArgumentNullException(nameof(intentions));
    }

    public bool IsSelectedLineInDraft => HasSelection && SelectedRow.DraftAmount > 0;
    public int MaxQuantity => HasSelection ? Math.Max(1, SelectedRow.MaxDraftAmount) : 1;

    /// <summary>Displayed total of the selected line at the chosen quantity, read from its row.</summary>
    public long LineTotal => HasSelection ? SelectedRow.TotalFor(Quantity) : 0;

    public bool CanChangeQuantity => HasSelection && ViewModel.CanEdit && SelectedRow.MaxDraftAmount > 1;
    public bool CanDecreaseQuantity => CanChangeQuantity && Quantity > 1;
    public bool CanIncreaseQuantity => CanChangeQuantity && Quantity < SelectedRow.MaxDraftAmount;

    public MerchantShopLineAction LineAction =>
        !HasSelection ? MerchantShopLineAction.None
        : IsSelectedLineInDraft ? MerchantShopLineAction.Update
        : MerchantShopLineAction.Add;

    public bool CanApplyLine => LineAction switch
    {
        MerchantShopLineAction.Add => SelectedRow.CanAdd && Quantity <= SelectedRow.MaxDraftAmount,
        MerchantShopLineAction.Update => ViewModel.CanEdit && Quantity != SelectedRow.DraftAmount,
        _ => false
    };

    public bool CanRemoveLine => IsSelectedLineInDraft && ViewModel.CanEdit;
    public bool CanConfirm => ViewModel.CanConfirm && !_confirmPending;
    public bool CanClear => ViewModel.CanClear;

    public bool IsSelected(MerchantShopRowViewModel row) =>
        HasSelection && row.LootId == _selectedLootId && row.IsMerchantStock == _isSelectedFromMerchant;

    /// <summary>
    /// Shows a new view model. The selection follows its row; the quantity resets to the draft
    /// amount whenever that line changed, and is kept within the row's limit otherwise.
    /// </summary>
    public void Present(MerchantShopViewModel viewModel)
    {
        ViewModel = viewModel ?? MerchantShopViewModel.Unavailable;
        _confirmPending = false;

        if (!HasSelection)
        {
            return;
        }

        if (!TryFindRow(_selectedLootId, _isSelectedFromMerchant, out MerchantShopRowViewModel row))
        {
            ClearSelection();
            return;
        }

        SelectedRow = row;
        if (row.DraftAmount != _syncedDraftAmount)
        {
            SyncQuantityWithDraft();
        }
        else
        {
            Quantity = Clamp(Quantity);
        }
    }

    /// <summary>Selects a merchant row for purchase or an Inventory row for sale.</summary>
    public bool Select(LootId lootId, bool isMerchantStock)
    {
        if (!TryFindRow(lootId, isMerchantStock, out MerchantShopRowViewModel row))
        {
            return false;
        }

        HasSelection = true;
        _selectedLootId = lootId;
        _isSelectedFromMerchant = isMerchantStock;
        SelectedRow = row;
        SyncQuantityWithDraft();
        return true;
    }

    public void ClearSelection()
    {
        HasSelection = false;
        SelectedRow = default;
        _selectedLootId = default;
        _syncedDraftAmount = 0;
        Quantity = 0;
    }

    public bool SetQuantity(int quantity)
    {
        if (!CanChangeQuantity)
        {
            return false;
        }

        int clamped = Clamp(quantity);
        bool changed = clamped != Quantity;
        Quantity = clamped;
        return changed;
    }

    public bool IncreaseQuantity() => SetQuantity(Quantity + 1);
    public bool DecreaseQuantity() => SetQuantity(Quantity - 1);

    /// <summary>Adds the selected line, or updates the amount of the line already in the draft.</summary>
    public bool ApplyLine()
    {
        if (!CanApplyLine)
        {
            return false;
        }

        MerchantShopRowViewModel row = SelectedRow;
        if (LineAction == MerchantShopLineAction.Add)
        {
            return row.IsMerchantStock
                ? _intentions.AddPurchase(row.LootId, Quantity)
                : _intentions.AddSale(row.LootId, Quantity);
        }

        return row.IsMerchantStock
            ? _intentions.SetPurchaseAmount(row.LootId, Quantity)
            : _intentions.SetSaleAmount(row.LootId, Quantity);
    }

    public bool RemoveLine()
    {
        if (!CanRemoveLine)
        {
            return false;
        }

        return SelectedRow.IsMerchantStock
            ? _intentions.RemovePurchase(SelectedRow.LootId)
            : _intentions.RemoveSale(SelectedRow.LootId);
    }

    public bool Clear() => CanClear && _intentions.ClearTrade();

    /// <summary>
    /// Confirms only when the view model allows it, and at most once until the next view model
    /// arrives, so repeated clicks never send a second confirmation.
    /// </summary>
    public bool Confirm()
    {
        if (!CanConfirm)
        {
            return false;
        }

        _confirmPending = true;
        bool sent = _intentions.ConfirmTrade();
        if (!sent)
        {
            _confirmPending = false;
        }

        return sent;
    }

    private void SyncQuantityWithDraft()
    {
        _syncedDraftAmount = SelectedRow.DraftAmount;
        Quantity = Clamp(SelectedRow.DraftAmount > 0 ? SelectedRow.DraftAmount : 1);
    }

    private int Clamp(int quantity) => Math.Min(Math.Max(1, quantity), MaxQuantity);

    private bool TryFindRow(LootId lootId, bool isMerchantStock, out MerchantShopRowViewModel found)
    {
        IReadOnlyList<MerchantShopRowViewModel> rows = isMerchantStock ? ViewModel.MerchantRows : ViewModel.InventoryRows;
        foreach (MerchantShopRowViewModel row in rows)
        {
            if (row.LootId == lootId)
            {
                found = row;
                return true;
            }
        }

        found = default;
        return false;
    }
}
