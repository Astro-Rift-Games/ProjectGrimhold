using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public class MerchantTradePreviewTests
{
    private const int Capacity = 3;

    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Bone = new LootId("bone");
    private static readonly LootId Hat = new LootId("arcane_mage_hat");
    private static readonly LootId Relic = new LootId("relic");
    private static readonly LootId Unknown = new LootId("unknown_item");

    private LootDefinitionCatalog _catalog;
    private Dictionary<LootId, int> _stock;
    private MerchantTradeDraft _draft;

    [SetUp]
    public void SetUp()
    {
        _catalog = MerchantTestContent.CreateCatalog(
            MerchantTestContent.CreateDefinition(Potion.Value, buyValue: 30, sellValue: 10, extractionValue: 500),
            MerchantTestContent.CreateDefinition(Bone.Value, buyValue: 5, sellValue: 2),
            MerchantTestContent.CreateDefinition(Hat.Value, buyValue: 100, sellValue: 40),
            MerchantTestContent.CreateDefinition(Relic.Value, buyValue: 0, sellValue: 25));
        _stock = new Dictionary<LootId, int>
        {
            { Potion, 5 },
            { Bone, MerchantStockItem.UnlimitedQuantity },
            { Hat, 1 }
        };
        _draft = new MerchantTradeDraft(() => new Guid("11111111-1111-1111-1111-111111111111"));
    }

    // --- Draft state ---

    [Test]
    public void EmptyDraft_CannotConfirm()
    {
        MerchantTradePreview preview = Calculate(Inventory(), currency: 100);

        Assert.That(preview.CanConfirm, Is.False);
        AssertBlocks(preview, MerchantTradeBlockReason.EmptyDraft);
        Assert.That(preview.Balance, Is.Zero);
        Assert.That(preview.ProjectedCurrency, Is.EqualTo(100));
    }

    [Test]
    public void InFlightDraft_CannotConfirmAgain()
    {
        _draft.TryAddPurchase(Bone, 1);
        _draft.TryBeginSubmission(out _);

        MerchantTradePreview preview = Calculate(Inventory(), currency: 100);

        Assert.That(preview.CanConfirm, Is.False);
        AssertBlocks(preview, MerchantTradeBlockReason.SubmissionInFlight);
        Assert.That(preview.PurchaseTotal, Is.EqualTo(5), "Values stay visible while in flight.");
    }

    // --- Totals and balance ---

    [Test]
    public void PurchaseOnly_UsesBuyValueAndAddsUnits()
    {
        _draft.TryAddPurchase(Potion, 2);

        MerchantTradePreview preview = Calculate(Inventory(), currency: 100);

        Assert.That(preview.CanConfirm, Is.True);
        Assert.That(preview.PurchaseTotal, Is.EqualTo(60), "Buy value, never extraction value.");
        Assert.That(preview.SaleTotal, Is.Zero);
        Assert.That(preview.Balance, Is.EqualTo(-60));
        Assert.That(preview.ProjectedCurrency, Is.EqualTo(40));
        AssertInventory(preview, (Potion, 2));
    }

    [Test]
    public void SaleOnly_UsesSellValueAndRemovesUnits()
    {
        _draft.TryAddSale(Potion, 3);

        MerchantTradePreview preview = Calculate(Inventory((Potion, 5)), currency: 0);

        Assert.That(preview.CanConfirm, Is.True);
        Assert.That(preview.SaleTotal, Is.EqualTo(30));
        Assert.That(preview.PurchaseTotal, Is.Zero);
        Assert.That(preview.Balance, Is.EqualTo(30));
        Assert.That(preview.ProjectedCurrency, Is.EqualTo(30));
        AssertInventory(preview, (Potion, 2));
    }

    [Test]
    public void PurchaseAndSale_ComputeTotalsAndBalanceTogether()
    {
        _draft.TryAddPurchase(Bone, 4);
        _draft.TryAddPurchase(Potion, 1);
        _draft.TryAddSale(Hat, 2);

        MerchantTradePreview preview = Calculate(Inventory((Hat, 2)), currency: 10);

        Assert.That(preview.PurchaseTotal, Is.EqualTo(50));
        Assert.That(preview.SaleTotal, Is.EqualTo(80));
        Assert.That(preview.Balance, Is.EqualTo(30));
        Assert.That(preview.ProjectedCurrency, Is.EqualTo(40));
        Assert.That(preview.CanConfirm, Is.True);
        AssertInventory(preview, (Bone, 4), (Potion, 1));
    }

    [Test]
    public void SaleFundsPurchase_OnTheFinalBalance()
    {
        _draft.TryAddPurchase(Hat, 1);
        _draft.TryAddSale(Potion, 3);

        MerchantTradePreview preview = Calculate(Inventory((Potion, 3)), currency: 80);

        Assert.That(preview.ProjectedCurrency, Is.EqualTo(10));
        Assert.That(preview.CanConfirm, Is.True, "80 + 30 covers 100 only when the sale is counted.");
    }

    [Test]
    public void InsufficientCurrency_BlocksConfirmAndReportsNegativeProjection()
    {
        _draft.TryAddPurchase(Hat, 1);

        MerchantTradePreview preview = Calculate(Inventory(), currency: 99);

        Assert.That(preview.ProjectedCurrency, Is.EqualTo(-1));
        AssertBlocks(preview, MerchantTradeBlockReason.InsufficientCurrency);
    }

    // --- Stock and ownership ---

    [Test]
    public void PurchaseAboveAvailableStock_IsBlocked()
    {
        _draft.TryAddPurchase(Hat, 2);

        MerchantTradePreview preview = Calculate(Inventory(), currency: 1000);

        AssertBlocks(preview, MerchantTradeBlockReason.InsufficientStock);
        Assert.That(preview.Blocks[0].LootId, Is.EqualTo(Hat));
    }

    [Test]
    public void PurchaseOfLastUnit_And_UnlimitedStock_AreAllowed()
    {
        _draft.TryAddPurchase(Hat, 1);
        _draft.TryAddPurchase(Bone, 10000);

        MerchantTradePreview preview = Calculate(Inventory(), currency: 1000000);

        Assert.That(preview.CanConfirm, Is.True);
    }

    [Test]
    public void PurchaseOfItemWithoutStock_IsBlocked()
    {
        _stock.Remove(Potion);
        _draft.TryAddPurchase(Potion, 1);

        MerchantTradePreview preview = Calculate(Inventory(), currency: 1000);

        AssertBlocks(preview, MerchantTradeBlockReason.InsufficientStock);
    }

    [Test]
    public void SaleAboveConfirmedOwnership_IsBlockedAndExcludedFromTotals()
    {
        _draft.TryAddSale(Potion, 4);

        MerchantTradePreview preview = Calculate(Inventory((Potion, 3)), currency: 0);

        AssertBlocks(preview, MerchantTradeBlockReason.InsufficientOwnedUnits);
        Assert.That(preview.SaleTotal, Is.Zero);
        AssertInventory(preview, (Potion, 3));
    }

    [Test]
    public void SameLootId_SaleUsesConfirmedUnitsAndSoldUnitsAreNotPurchasable()
    {
        _stock[Potion] = 1;
        _draft.TryAddSale(Potion, 3);
        _draft.TryAddPurchase(Potion, 2);

        MerchantTradePreview preview = Calculate(Inventory((Potion, 3)), currency: 100);

        AssertBlocks(preview, MerchantTradeBlockReason.InsufficientStock);
        Assert.That(preview.SaleTotal, Is.EqualTo(30));
        Assert.That(preview.PurchaseTotal, Is.EqualTo(60));
        AssertInventory(preview, (Potion, 2));
    }

    [Test]
    public void SameLootId_WithinStockAndOwnership_ProjectsNetAmount()
    {
        _draft.TryAddSale(Potion, 2);
        _draft.TryAddPurchase(Potion, 5);

        MerchantTradePreview preview = Calculate(Inventory((Potion, 2)), currency: 200);

        Assert.That(preview.CanConfirm, Is.True);
        Assert.That(preview.Balance, Is.EqualTo(20 - 150));
        AssertInventory(preview, (Potion, 5));
    }

    // --- Capacity ---

    [Test]
    public void PurchaseIntoExistingStack_UsesNoNewSlot()
    {
        _draft.TryAddPurchase(Bone, 3);

        MerchantTradePreview preview = Calculate(Inventory((Bone, 1), (Potion, 1), (Hat, 1)), currency: 100);

        Assert.That(preview.CanConfirm, Is.True);
        AssertInventory(preview, (Bone, 4), (Potion, 1), (Hat, 1));
    }

    [Test]
    public void PurchaseNeedingNewSlotBeyondCapacity_IsBlocked()
    {
        _draft.TryAddPurchase(Potion, 1);

        MerchantTradePreview preview = Calculate(Inventory((Bone, 1), (Hat, 1), (Relic, 1)), currency: 100);

        AssertBlocks(preview, MerchantTradeBlockReason.CapacityExceeded);
        Assert.That(preview.ProjectedInventory.Count, Is.EqualTo(4), "The projection still shows the result.");
    }

    [Test]
    public void SaleOfWholeStack_FreesCapacityForPurchase()
    {
        _draft.TryAddSale(Relic, 1);
        _draft.TryAddPurchase(Potion, 1);

        MerchantTradePreview preview = Calculate(Inventory((Bone, 1), (Hat, 1), (Relic, 1)), currency: 100);

        Assert.That(preview.CanConfirm, Is.True);
        AssertInventory(preview, (Bone, 1), (Hat, 1), (Potion, 1));
    }

    [Test]
    public void OccupiedSlots_AreReportedBeforeAndAfterTheTrade()
    {
        _draft.TryAddSale(Relic, 1);
        _draft.TryAddPurchase(Potion, 1);
        _draft.TryAddPurchase(Hat, 1);

        MerchantTradePreview preview = Calculate(Inventory((Bone, 1), (Bone, 2), (Relic, 1)), currency: 1000);

        Assert.That(preview.ConfirmedOccupiedSlots, Is.EqualTo(2), "Stacks of one loot share a slot.");
        Assert.That(preview.ProjectedOccupiedSlots, Is.EqualTo(3));
        Assert.That(preview.SlotCapacity, Is.EqualTo(Capacity));
    }

    [Test]
    public void PartialSale_DoesNotFreeItsSlot()
    {
        _draft.TryAddSale(Relic, 1);
        _draft.TryAddPurchase(Potion, 1);

        MerchantTradePreview preview = Calculate(Inventory((Bone, 1), (Hat, 1), (Relic, 2)), currency: 100);

        AssertBlocks(preview, MerchantTradeBlockReason.CapacityExceeded);
    }

    // --- Configuration and overflow ---

    [Test]
    public void UnknownItem_IsBlockedForPurchaseAndSale()
    {
        _draft.TryAddPurchase(Unknown, 1);
        _draft.TryAddSale(Unknown, 1);

        MerchantTradePreview preview = Calculate(Inventory((Unknown, 1)), currency: 100);

        Assert.That(preview.Blocks.Count(b => b.Reason == MerchantTradeBlockReason.UnknownItem && b.LootId == Unknown), Is.EqualTo(2));
        Assert.That(preview.PurchaseTotal, Is.Zero);
        Assert.That(preview.SaleTotal, Is.Zero);
    }

    [Test]
    public void ItemWithoutBuyValue_IsNotPurchasableButSellable()
    {
        _stock[Relic] = 5;
        _draft.TryAddPurchase(Relic, 1);

        MerchantTradePreview purchase = Calculate(Inventory(), currency: 100);
        AssertBlocks(purchase, MerchantTradeBlockReason.NotPurchasable);

        _draft.TryClear();
        _draft.TryAddSale(Relic, 1);
        MerchantTradePreview sale = Calculate(Inventory((Relic, 1)), currency: 100);
        Assert.That(sale.CanConfirm, Is.True);
        Assert.That(sale.SaleTotal, Is.EqualTo(25));
    }

    [Test]
    public void ProjectedCurrencyOverflow_IsBlocked()
    {
        _draft.TryAddSale(Potion, 1);

        MerchantTradePreview preview = Calculate(Inventory((Potion, 1)), currency: long.MaxValue - 5);

        AssertBlocks(preview, MerchantTradeBlockReason.Overflow);
        Assert.That(preview.ProjectedCurrency, Is.Zero);
    }

    [Test]
    public void PurchaseTotalOverflow_IsBlocked()
    {
        _catalog = MerchantTestContent.CreateCatalog(
            MerchantTestContent.CreateDefinition(Potion.Value, int.MaxValue, 1),
            MerchantTestContent.CreateDefinition(Bone.Value, int.MaxValue, 1),
            MerchantTestContent.CreateDefinition(Hat.Value, int.MaxValue, 1));
        _stock[Potion] = MerchantStockItem.UnlimitedQuantity;
        _stock[Hat] = MerchantStockItem.UnlimitedQuantity;
        _draft.TryAddPurchase(Potion, int.MaxValue);
        _draft.TryAddPurchase(Bone, int.MaxValue);
        _draft.TryAddPurchase(Hat, int.MaxValue);

        MerchantTradePreview preview = Calculate(Inventory(), currency: long.MaxValue);

        AssertBlocks(preview, MerchantTradeBlockReason.Overflow);
        Assert.That(preview.PurchaseTotal, Is.Zero);
    }

    [Test]
    public void StackAmountOverflow_IsBlocked()
    {
        _draft.TryAddPurchase(Bone, 1);

        MerchantTradePreview preview = Calculate(Inventory((Bone, int.MaxValue)), currency: 100);

        AssertBlocks(preview, MerchantTradeBlockReason.Overflow);
        Assert.That(preview.Blocks[0].LootId, Is.EqualTo(Bone));
    }

    // --- Side effects ---

    [Test]
    public void Calculate_MutatesNoInput()
    {
        _draft.TryAddPurchase(Potion, 2);
        _draft.TryAddSale(Hat, 1);
        _draft.TryBeginSubmission(out Guid requestId);
        _draft.MarkSubmissionRejectedOrFailed();
        var inventory = new List<LootEntry> { new LootEntry(Hat, 1) };
        var stockBefore = new Dictionary<LootId, int>(_stock);

        MerchantTradePreview preview = Calculate(inventory, currency: 100);

        Assert.That(preview.CanConfirm, Is.True);
        Assert.That(inventory, Is.EqualTo(new[] { new LootEntry(Hat, 1) }));
        Assert.That(_stock, Is.EqualTo(stockBefore));
        Assert.That(_draft.Purchases, Is.EqualTo(new[] { new MerchantTradeLine(Potion, 2) }));
        Assert.That(_draft.Sales, Is.EqualTo(new[] { new MerchantTradeLine(Hat, 1) }));
        Assert.That(_draft.RequestId, Is.EqualTo(requestId));
        Assert.That(_draft.IsInFlight, Is.False);
    }

    [Test]
    public void Preview_IsImmutable()
    {
        _draft.TryAddPurchase(Potion, 1);

        MerchantTradePreview preview = Calculate(Inventory(), currency: 0);

        Assert.That(preview.Blocks, Is.Not.InstanceOf<List<MerchantTradeBlock>>());
        Assert.Throws<NotSupportedException>(() => ((IList<LootEntry>)preview.ProjectedInventory).Add(new LootEntry(Bone, 1)));
    }

    private MerchantTradePreview Calculate(IReadOnlyList<LootEntry> inventory, long currency) =>
        MerchantTradePreview.Calculate(
            inventory,
            Capacity,
            currency,
            _draft,
            lootId => _stock.TryGetValue(lootId, out int quantity) ? quantity : 0,
            _catalog);

    private static IReadOnlyList<LootEntry> Inventory(params (LootId lootId, int amount)[] items) =>
        items.Select(item => new LootEntry(item.lootId, item.amount)).ToList();

    private static void AssertBlocks(MerchantTradePreview preview, params MerchantTradeBlockReason[] expected)
    {
        Assert.That(preview.CanConfirm, Is.False);
        Assert.That(preview.Blocks.Select(block => block.Reason), Is.EqualTo(expected));
    }

    private static void AssertInventory(MerchantTradePreview preview, params (LootId lootId, int amount)[] expected)
    {
        Assert.That(preview.ProjectedInventory, Is.EqualTo(Inventory(expected)));
    }
}
