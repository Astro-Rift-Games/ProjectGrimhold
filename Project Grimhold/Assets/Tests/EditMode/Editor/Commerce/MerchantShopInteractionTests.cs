using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public class MerchantShopInteractionTests
{
    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Hat = new LootId("placeholder_helmet");

    private FakeIntentions _intentions;
    private MerchantShopInteraction _interaction;

    [SetUp]
    public void SetUp()
    {
        _intentions = new FakeIntentions();
        _interaction = new MerchantShopInteraction(_intentions);
        _interaction.Present(View(MerchantRow(Potion, draft: 0, max: 5), InventoryRow(Hat, draft: 0, max: 2)));
    }

    // --- Selection ---

    [Test]
    public void SelectingStockRow_SelectsItForPurchaseWithHighlight()
    {
        Assert.That(_interaction.Select(Potion, isMerchantStock: true), Is.True);

        Assert.That(_interaction.HasSelection, Is.True);
        Assert.That(_interaction.SelectedRow.IsMerchantStock, Is.True);
        Assert.That(_interaction.IsSelected(_interaction.ViewModel.MerchantRows[0]), Is.True);
        Assert.That(_interaction.IsSelected(_interaction.ViewModel.InventoryRows[0]), Is.False);
        Assert.That(_interaction.LineAction, Is.EqualTo(MerchantShopLineAction.Add));
        Assert.That(_interaction.Quantity, Is.EqualTo(1));
    }

    [Test]
    public void SelectingInventoryRow_SelectsItForSale()
    {
        _interaction.Select(Hat, isMerchantStock: false);

        Assert.That(_interaction.SelectedRow.IsMerchantStock, Is.False);
        Assert.That(_interaction.IsSelected(_interaction.ViewModel.InventoryRows[0]), Is.True);
    }

    [Test]
    public void SelectingUnknownRow_KeepsNoSelection()
    {
        Assert.That(_interaction.Select(Hat, isMerchantStock: true), Is.False);
        Assert.That(_interaction.HasSelection, Is.False);
        Assert.That(_interaction.LineAction, Is.EqualTo(MerchantShopLineAction.None));
        Assert.That(_interaction.CanApplyLine, Is.False);
    }

    [Test]
    public void SelectedRowLeavingTheViewModel_ClearsTheSelection()
    {
        _interaction.Select(Hat, isMerchantStock: false);

        _interaction.Present(View(MerchantRow(Potion, draft: 0, max: 5)));

        Assert.That(_interaction.HasSelection, Is.False);
    }

    // --- Quantity ---

    [Test]
    public void Quantity_IsClampedToTheRowLimit()
    {
        _interaction.Select(Potion, isMerchantStock: true);

        Assert.That(_interaction.SetQuantity(9), Is.True);
        Assert.That(_interaction.Quantity, Is.EqualTo(5));
        Assert.That(_interaction.CanIncreaseQuantity, Is.False);

        _interaction.SetQuantity(0);
        Assert.That(_interaction.Quantity, Is.EqualTo(1));
        Assert.That(_interaction.CanDecreaseQuantity, Is.False);

        Assert.That(_interaction.IncreaseQuantity(), Is.True);
        Assert.That(_interaction.Quantity, Is.EqualTo(2));
    }

    [Test]
    public void ChangingQuantity_RaisesNoIntention()
    {
        _interaction.Select(Potion, isMerchantStock: true);
        _interaction.SetQuantity(3);

        Assert.That(_intentions.Calls, Is.Empty, "Quantity is local until the line is added or updated.");
    }

    [Test]
    public void SingleUnitRow_CannotChangeQuantity()
    {
        _interaction.Present(View(MerchantRow(Potion, draft: 0, max: 1)));
        _interaction.Select(Potion, isMerchantStock: true);

        Assert.That(_interaction.CanChangeQuantity, Is.False);
        Assert.That(_interaction.SetQuantity(2), Is.False);
    }

    // --- Add, update, remove ---

    [Test]
    public void Apply_AddsPurchaseLineWithChosenQuantity()
    {
        _interaction.Select(Potion, isMerchantStock: true);
        _interaction.SetQuantity(3);

        Assert.That(_interaction.ApplyLine(), Is.True);

        Assert.That(_intentions.Calls, Is.EqualTo(new[] { "AddPurchase healthpotion 3" }));
    }

    [Test]
    public void Apply_AddsSaleLineForInventoryRow()
    {
        _interaction.Select(Hat, isMerchantStock: false);
        _interaction.SetQuantity(2);

        _interaction.ApplyLine();

        Assert.That(_intentions.Calls, Is.EqualTo(new[] { "AddSale placeholder_helmet 2" }));
    }

    [Test]
    public void LineAlreadyInDraft_IsUpdatedInsteadOfDuplicated()
    {
        _interaction.Select(Potion, isMerchantStock: true);
        _interaction.Present(View(MerchantRow(Potion, draft: 2, max: 5)));

        Assert.That(_interaction.LineAction, Is.EqualTo(MerchantShopLineAction.Update));
        Assert.That(_interaction.Quantity, Is.EqualTo(2), "The quantity follows the draft line.");
        Assert.That(_interaction.CanApplyLine, Is.False, "Nothing to update at the same amount.");

        _interaction.SetQuantity(4);
        Assert.That(_interaction.ApplyLine(), Is.True);

        Assert.That(_intentions.Calls, Is.EqualTo(new[] { "SetPurchaseAmount healthpotion 4" }));
    }

    [Test]
    public void UpdatingSaleLine_SetsItsAmount()
    {
        _interaction.Present(View(InventoryRow(Hat, draft: 2, max: 2)));
        _interaction.Select(Hat, isMerchantStock: false);
        _interaction.SetQuantity(1);

        _interaction.ApplyLine();

        Assert.That(_intentions.Calls, Is.EqualTo(new[] { "SetSaleAmount placeholder_helmet 1" }));
    }

    [Test]
    public void Remove_OnlyAppliesToLinesInTheDraft()
    {
        _interaction.Select(Potion, isMerchantStock: true);
        Assert.That(_interaction.CanRemoveLine, Is.False);
        Assert.That(_interaction.RemoveLine(), Is.False);

        _interaction.Present(View(MerchantRow(Potion, draft: 2, max: 5)));
        Assert.That(_interaction.RemoveLine(), Is.True);

        Assert.That(_intentions.Calls, Is.EqualTo(new[] { "RemovePurchase healthpotion" }));
    }

    [Test]
    public void RemovingSaleLine_RaisesRemoveSale()
    {
        _interaction.Present(View(InventoryRow(Hat, draft: 1, max: 2)));
        _interaction.Select(Hat, isMerchantStock: false);

        _interaction.RemoveLine();

        Assert.That(_intentions.Calls, Is.EqualTo(new[] { "RemoveSale placeholder_helmet" }));
    }

    [Test]
    public void RemovedOrClearedLine_ResetsQuantityAndAction()
    {
        _interaction.Present(View(MerchantRow(Potion, draft: 3, max: 5)));
        _interaction.Select(Potion, isMerchantStock: true);

        _interaction.Present(View(MerchantRow(Potion, draft: 0, max: 5)));

        Assert.That(_interaction.LineAction, Is.EqualTo(MerchantShopLineAction.Add));
        Assert.That(_interaction.Quantity, Is.EqualTo(1));
    }

    [Test]
    public void UnchangedLine_KeepsChosenQuantityAcrossRefreshes()
    {
        _interaction.Select(Potion, isMerchantStock: true);
        _interaction.SetQuantity(4);

        _interaction.Present(View(MerchantRow(Potion, draft: 0, max: 3)));

        Assert.That(_interaction.Quantity, Is.EqualTo(3), "A stock refresh only clamps the chosen quantity.");
    }

    [Test]
    public void RowThatCannotBeAdded_DisablesAdd()
    {
        _interaction.Present(View(MerchantRow(Potion, draft: 0, max: 0, canAdd: false)));
        _interaction.Select(Potion, isMerchantStock: true);

        Assert.That(_interaction.CanApplyLine, Is.False);
        Assert.That(_interaction.ApplyLine(), Is.False);
        Assert.That(_intentions.Calls, Is.Empty);
    }

    // --- Confirm and clear ---

    [Test]
    public void Confirm_FollowsOnlyTheViewModel()
    {
        _interaction.Present(View(canConfirm: false, hasDraft: true, rows: new[] { MerchantRow(Potion, draft: 1, max: 5) }));
        Assert.That(_interaction.CanConfirm, Is.False);
        Assert.That(_interaction.Confirm(), Is.False);

        _interaction.Present(View(canConfirm: true, hasDraft: true, rows: new[] { MerchantRow(Potion, draft: 1, max: 5) }));
        Assert.That(_interaction.CanConfirm, Is.True);
        Assert.That(_interaction.Confirm(), Is.True);

        Assert.That(_intentions.Calls, Is.EqualTo(new[] { "ConfirmTrade" }));
    }

    [Test]
    public void RepeatedConfirm_IsSentOnceUntilTheNextViewModel()
    {
        _interaction.Present(View(canConfirm: true, hasDraft: true, rows: new[] { MerchantRow(Potion, draft: 1, max: 5) }));

        _interaction.Confirm();
        Assert.That(_interaction.CanConfirm, Is.False);
        Assert.That(_interaction.Confirm(), Is.False);

        Assert.That(_intentions.Calls, Is.EqualTo(new[] { "ConfirmTrade" }));
    }

    [Test]
    public void RefusedConfirm_CanBeRetried()
    {
        _intentions.Result = false;
        _interaction.Present(View(canConfirm: true, hasDraft: true, rows: new[] { MerchantRow(Potion, draft: 1, max: 5) }));

        _interaction.Confirm();

        Assert.That(_interaction.CanConfirm, Is.True);
    }

    [Test]
    public void Clear_EmptiesTheDraftOnlyWhenAllowed()
    {
        Assert.That(_interaction.Clear(), Is.False, "Nothing to clear.");

        _interaction.Present(View(hasDraft: true, rows: new[] { MerchantRow(Potion, draft: 1, max: 5) }));
        Assert.That(_interaction.CanClear, Is.True);
        Assert.That(_interaction.Clear(), Is.True);

        Assert.That(_intentions.Calls, Is.EqualTo(new[] { "ClearTrade" }));
    }

    // --- In flight ---

    [Test]
    public void InFlight_BlocksEveryDraftEditAndConfirm()
    {
        _interaction.Present(View(hasDraft: true, isInFlight: true, rows: new[]
        {
            MerchantRow(Potion, draft: 2, max: 5, canAdd: false),
            InventoryRow(Hat, draft: 0, max: 2, canAdd: false)
        }));
        _interaction.Select(Potion, isMerchantStock: true);

        Assert.That(_interaction.CanChangeQuantity, Is.False);
        Assert.That(_interaction.SetQuantity(4), Is.False);
        Assert.That(_interaction.CanApplyLine, Is.False);
        Assert.That(_interaction.CanRemoveLine, Is.False);
        Assert.That(_interaction.CanClear, Is.False);
        Assert.That(_interaction.CanConfirm, Is.False);

        _interaction.ApplyLine();
        _interaction.RemoveLine();
        _interaction.Clear();
        _interaction.Confirm();
        _interaction.Select(Hat, isMerchantStock: false);
        _interaction.ApplyLine();

        Assert.That(_intentions.Calls, Is.Empty);
    }

    [Test]
    public void UnavailableViewModel_AllowsNothing()
    {
        _interaction.Present(null);

        Assert.That(_interaction.ViewModel.IsAvailable, Is.False);
        Assert.That(_interaction.Select(Potion, isMerchantStock: true), Is.False);
        Assert.That(_interaction.CanConfirm, Is.False);
        Assert.That(_interaction.CanClear, Is.False);
    }

    // --- Helpers ---

    private static MerchantShopRowViewModel MerchantRow(LootId lootId, int draft, int max, bool canAdd = true) =>
        new MerchantShopRowViewModel(null, lootId, true, 30, max, draft, 30L * draft, canAdd && draft < max, max);

    private static MerchantShopRowViewModel InventoryRow(LootId lootId, int draft, int max, bool canAdd = true) =>
        new MerchantShopRowViewModel(null, lootId, false, 5, max, draft, 5L * draft, canAdd && draft < max, max);

    private static MerchantShopViewModel View(params MerchantShopRowViewModel[] rows) => View(false, false, false, rows);

    private static MerchantShopViewModel View(
        bool canConfirm = false,
        bool hasDraft = false,
        bool isInFlight = false,
        params MerchantShopRowViewModel[] rows)
    {
        var merchant = new List<MerchantShopRowViewModel>();
        var inventory = new List<MerchantShopRowViewModel>();
        foreach (MerchantShopRowViewModel row in rows)
        {
            (row.IsMerchantStock ? merchant : inventory).Add(row);
        }

        var blocks = new List<MerchantTradeBlock>();
        if (isInFlight) blocks.Add(new MerchantTradeBlock(MerchantTradeBlockReason.SubmissionInFlight));

        return new MerchantShopViewModel(
            true, 100, 100, 0, 0, 0, 1, 1, 10, hasDraft, isInFlight, canConfirm && !isInFlight, blocks, merchant, inventory);
    }

    private sealed class FakeIntentions : IMerchantShopIntentions
    {
        public List<string> Calls { get; } = new List<string>();
        public bool Result { get; set; } = true;

        public bool AddPurchase(LootId lootId, int amount) => Record($"AddPurchase {lootId.Value} {amount}");
        public bool SetPurchaseAmount(LootId lootId, int amount) => Record($"SetPurchaseAmount {lootId.Value} {amount}");
        public bool RemovePurchase(LootId lootId) => Record($"RemovePurchase {lootId.Value}");
        public bool AddSale(LootId lootId, int amount) => Record($"AddSale {lootId.Value} {amount}");
        public bool SetSaleAmount(LootId lootId, int amount) => Record($"SetSaleAmount {lootId.Value} {amount}");
        public bool RemoveSale(LootId lootId) => Record($"RemoveSale {lootId.Value}");
        public bool ClearTrade() => Record("ClearTrade");
        public bool ConfirmTrade() => Record("ConfirmTrade");

        private bool Record(string call)
        {
            Calls.Add(call);
            return Result;
        }
    }
}
