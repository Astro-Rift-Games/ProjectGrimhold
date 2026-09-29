using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

/// <summary>
/// Renders the real Merchant Shop prefab in EditMode. Awake does not run here, so button listeners
/// are not wired; selection is raised through the same handler the rows call.
/// </summary>
public class MerchantShopUIRenderTests
{
    private const string MerchantShopPrefabPath = "Assets/Prefabs/UI/MerchantShopUI.prefab";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Hat = new LootId("placeholder_helmet");

    private MerchantShopUI _ui;
    private LootDefinition _potion;
    private LootDefinition _hat;
    private RecordingIntentions _intentions;
    private Texture2D _iconTexture;

    [SetUp]
    public void SetUp()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<MerchantShopUI>(MerchantShopPrefabPath);
        Assert.That(prefab, Is.Not.Null, MerchantShopPrefabPath);
        _ui = Object.Instantiate(prefab);
        _potion = MerchantTestContent.CreateDefinition(Potion.Value, buyValue: 30, sellValue: 20);
        _hat = MerchantTestContent.CreateDefinition(Hat.Value, buyValue: 10, sellValue: 5);
        _iconTexture = new Texture2D(2, 2);
        SetIcon(_potion);
        SetIcon(_hat);
        _intentions = new RecordingIntentions();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_ui.gameObject);
        Object.DestroyImmediate(_potion.Icon);
        Object.DestroyImmediate(_hat.Icon);
        Object.DestroyImmediate(_potion);
        Object.DestroyImmediate(_hat);
        Object.DestroyImmediate(_iconTexture);
    }

    private void SetIcon(LootDefinition definition)
    {
        var serialized = new SerializedObject(definition);
        serialized.FindProperty("_icon").objectReferenceValue = Sprite.Create(_iconTexture, new Rect(0, 0, 2, 2), Vector2.zero);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [Test]
    public void ViewModelPresentedBeforeBind_IsRendered()
    {
        _ui.Present(View(potionDraft: 2, projectedCurrency: 50, canConfirm: true));
        _ui.Bind(_intentions);

        Assert.That(Text("_currencyText"), Is.EqualTo("100 -> 50"));
        Assert.That(Text("_purchaseTotalText"), Is.EqualTo("Compra: 60"));
        Assert.That(Text("_saleTotalText"), Is.EqualTo("Venta: 10"));
        Assert.That(Text("_balanceText"), Is.EqualTo("Balance: -50 a pagar"));
        Assert.That(Text("_capacityText"), Is.EqualTo("Espacio: 2/20 -> 3/20"));
        Assert.That(Text("_feedbackText"), Is.Empty);
        Assert.That(Field<Button>("_confirmButton").interactable, Is.True);
        Assert.That(Field<Button>("_clearButton").interactable, Is.True);
        Assert.That(Rows("_merchantRows").Count, Is.EqualTo(1));
        Assert.That(Rows("_inventoryRows").Count, Is.EqualTo(20), "Every Inventory slot of the capacity.");
        Assert.That(Field<GameObject>("_centerPanelRoot").activeSelf, Is.False, "Nothing is selected yet.");
    }

    [Test]
    public void BlockedPreview_DisablesConfirmAndShowsItsMessage()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 2, projectedCurrency: -40, canConfirm: false,
            new MerchantTradeBlock(MerchantTradeBlockReason.InsufficientCurrency)));

        Assert.That(Field<Button>("_confirmButton").interactable, Is.False);
        Assert.That(Text("_feedbackText"), Is.EqualTo("Oro insuficiente: faltan 40"));
    }

    [Test]
    public void SelectingARow_HighlightsItAndFillsTheCenterPanel()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 2, projectedCurrency: 50, canConfirm: true));

        Select(Potion, isMerchantStock: true);

        Assert.That(Field<GameObject>("_centerPanelRoot").activeSelf, Is.True);
        Assert.That(Highlight(Rows("_merchantRows")[0]).activeSelf, Is.True);
        Assert.That(Highlight(Rows("_inventoryRows")[0]).activeSelf, Is.False);
        Assert.That(Text("_detailName"), Is.EqualTo(_potion.name));
        Assert.That(Text("_detailPropertiesText"), Is.EqualTo(EquipmentTooltipPresentationBuilder.Build(_potion).Body));
        Assert.That(Text("_unitPriceText"), Is.EqualTo("30"));
        Assert.That(Text("_lineTotalText"), Is.EqualTo("60"));
        Assert.That(Text("_lineStateText"), Is.EqualTo("Compra agregada: 2"));
        Assert.That(Text("_quantityText"), Is.EqualTo("2"));
        Assert.That(Text("_actionButtonText"), Is.EqualTo("Actualizar"));
        Assert.That(Field<Button>("_actionButton").interactable, Is.False, "Same amount: nothing to update.");
        Assert.That(Field<Button>("_removeLineButton").interactable, Is.True);
    }

    [Test]
    public void SelectingARowOutsideTheDraft_OffersAdd()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 0, projectedCurrency: 100, canConfirm: false,
            new MerchantTradeBlock(MerchantTradeBlockReason.EmptyDraft)));

        Select(Hat, isMerchantStock: false);

        Assert.That(Text("_lineStateText"), Is.EqualTo("Sin agregar"));
        Assert.That(Text("_lineTotalText"), Is.EqualTo("5"));
        Assert.That(Text("_actionButtonText"), Is.EqualTo("Agregar"));
        Assert.That(Field<Button>("_actionButton").interactable, Is.True);
        Assert.That(Field<Button>("_removeLineButton").interactable, Is.False);
        Assert.That(Field<Button>("_clearButton").interactable, Is.False);
    }

    [Test]
    public void SaleLineInTheDraft_IsDescribedAsASale()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 0, projectedCurrency: 105, canConfirm: true, hatDraft: 1));

        Select(Hat, isMerchantStock: false);

        Assert.That(Text("_lineStateText"), Is.EqualTo("Venta agregada: 1"));
        Assert.That(Text("_actionButtonText"), Is.EqualTo("Actualizar"));
        Assert.That(Field<Button>("_removeLineButton").interactable, Is.True);
    }

    [Test]
    public void ChangingTheQuantity_UpdatesTheLineTotalFromTheRow()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 0, projectedCurrency: 100, canConfirm: false,
            new MerchantTradeBlock(MerchantTradeBlockReason.EmptyDraft)));
        Select(Hat, isMerchantStock: false);

        typeof(MerchantShopUI).GetMethod("OnIncreaseQuantityClicked", PrivateInstance).Invoke(_ui, null);

        Assert.That(Text("_quantityText"), Is.EqualTo("2"));
        Assert.That(Text("_lineTotalText"), Is.EqualTo("10"));
    }

    [TestCase(110, "Balance: +10 a favor")]
    [TestCase(100, "Balance: 0 sin cambio")]
    [TestCase(40, "Balance: -60 a pagar")]
    public void Balance_NamesItsDirectionBesidesItsSign(long projectedCurrency, string expected)
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 0, projectedCurrency, canConfirm: true));

        Assert.That(Text("_balanceText"), Is.EqualTo(expected));
    }

    [Test]
    public void InventoryGrid_FillsItsCapacityWithInertEmptySlots()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 2, projectedCurrency: 50, canConfirm: true));

        List<StoreItemUI> slots = Rows("_inventoryRows");
        Assert.That(slots.Count, Is.EqualTo(20));
        Assert.That(slots[0].IsEmpty, Is.False);
        Assert.That(slots[0].LootId, Is.EqualTo(Hat));

        StoreItemUI empty = slots[19];
        Assert.That(empty.IsEmpty, Is.True);
        Assert.That(SlotField<Image>(empty, "_iconImage").enabled, Is.False);
        Assert.That(SlotText(empty, "_amountText"), Is.Empty);
        Assert.That(SlotField<GameObject>(empty, "_priceRoot").activeSelf, Is.False);
        Assert.That(SlotField<GameObject>(empty, "_draftBadge").activeSelf, Is.False);
        Assert.That(Highlight(empty).activeSelf, Is.False);
        Assert.That(SlotField<Button>(empty, "_selectButton").enabled, Is.False, "An empty slot receives no pointer events.");

        typeof(StoreItemUI).GetMethod("OnSelectButtonClicked", PrivateInstance).Invoke(empty, null);
        Assert.That(Field<GameObject>("_centerPanelRoot").activeSelf, Is.False, "An empty slot raises no selection.");
        Assert.That(Rows("_merchantRows").Count, Is.EqualTo(1), "Merchant stock is never padded.");
    }

    [Test]
    public void InventoryGrid_ReusesSlotsWhenARowLeaves()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 0, projectedCurrency: 110, canConfirm: true, hatDraft: 1));
        StoreItemUI first = Rows("_inventoryRows")[0];

        _ui.Present(new MerchantShopViewModel(true, 100, 100, 0, 0, 0, 0, 0, 20, false, false, false,
            new[] { new MerchantTradeBlock(MerchantTradeBlockReason.EmptyDraft) },
            View(potionDraft: 0, projectedCurrency: 100, canConfirm: false).MerchantRows, new List<MerchantShopRowViewModel>()));

        Assert.That(Rows("_inventoryRows").Count, Is.EqualTo(20));
        Assert.That(Rows("_inventoryRows")[0], Is.SameAs(first));
        Assert.That(first.IsEmpty, Is.True, "The slot left by the sold row becomes empty.");
    }

    [Test]
    public void InFlight_DisablesEveryEditControl()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 2, projectedCurrency: 50, canConfirm: false,
            new MerchantTradeBlock(MerchantTradeBlockReason.SubmissionInFlight)));
        Select(Potion, isMerchantStock: true);

        Assert.That(Text("_feedbackText"), Is.EqualTo("Transacción en curso..."));
        foreach (string button in new[] { "_confirmButton", "_clearButton", "_actionButton", "_removeLineButton", "_increaseQtyButton", "_decreaseQtyButton" })
        {
            Assert.That(Field<Button>(button).interactable, Is.False, button);
        }

        Assert.That(Field<Slider>("_quantitySlider").interactable, Is.False);
    }

    [Test]
    public void TradeResult_IsShownUntilTheNextEdit()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 0, projectedCurrency: 100, canConfirm: false,
            new MerchantTradeBlock(MerchantTradeBlockReason.EmptyDraft)));

        _ui.PresentResult(MerchantTransactionResult.Success);
        Assert.That(Text("_feedbackText"), Is.EqualTo("Transacción completada"));

        typeof(MerchantShopUI).GetMethod("OnClearClicked", PrivateInstance).Invoke(_ui, null);
        _ui.Present(View(potionDraft: 0, projectedCurrency: 100, canConfirm: false,
            new MerchantTradeBlock(MerchantTradeBlockReason.EmptyDraft)));
        Assert.That(Text("_feedbackText"), Is.EqualTo("Agrega objetos a la transacción"));
    }

    [Test]
    public void WithoutDraft_FooterShowsGoldAndSlotsProjectedToThemselves()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 0, projectedCurrency: 100, canConfirm: false,
            new MerchantTradeBlock(MerchantTradeBlockReason.EmptyDraft)));

        Assert.That(Text("_currencyText"), Is.EqualTo("100 -> 100"));
        Assert.That(Text("_capacityText"), Is.EqualTo("Espacio: 2/20 -> 3/20"));
        Assert.That(Text("_feedbackText"), Is.EqualTo("Agrega objetos a la transacción"));
    }

    [Test]
    public void MerchantSlot_ShowsIconPriceStockAndPurchaseBadge()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 2, projectedCurrency: 50, canConfirm: true));

        StoreItemUI slot = Rows("_merchantRows")[0];
        Assert.That(SlotField<Image>(slot, "_iconImage").sprite, Is.SameAs(_potion.Icon));
        Assert.That(SlotField<GameObject>(slot, "_priceRoot").activeSelf, Is.True);
        Assert.That(SlotText(slot, "_priceText"), Is.EqualTo("30"));
        Assert.That(SlotText(slot, "_amountText"), Is.EqualTo("3"), "Stock stays the available amount.");
        Assert.That(SlotField<GameObject>(slot, "_draftBadge").activeSelf, Is.True);
        Assert.That(SlotText(slot, "_draftAmountText"), Is.EqualTo("+2"));
        Assert.That(Highlight(slot).activeSelf, Is.False);
    }

    [Test]
    public void InventorySlot_ShowsOwnedAmountWithoutPriceAndASeparateSaleBadge()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 0, projectedCurrency: 110, canConfirm: true, hatDraft: 1));

        StoreItemUI slot = Rows("_inventoryRows")[0];
        Assert.That(SlotField<Image>(slot, "_iconImage").sprite, Is.SameAs(_hat.Icon));
        Assert.That(SlotField<GameObject>(slot, "_priceRoot").activeSelf, Is.False);
        Assert.That(SlotText(slot, "_amountText"), Is.EqualTo("2"), "Owned units are never replaced by the projection.");
        Assert.That(SlotField<GameObject>(slot, "_draftBadge").activeSelf, Is.True);
        Assert.That(SlotText(slot, "_draftAmountText"), Is.EqualTo("-1"));
        Assert.That(SlotField<GameObject>(Rows("_merchantRows")[0], "_draftBadge").activeSelf, Is.False, "No purchase line.");
    }

    [Test]
    public void DraftBadge_FollowsTheDraftAndStaysWhileInFlight()
    {
        _ui.Bind(_intentions);
        _ui.Present(View(potionDraft: 2, projectedCurrency: 50, canConfirm: true));
        _ui.Present(View(potionDraft: 3, projectedCurrency: 20, canConfirm: true));
        StoreItemUI slot = Rows("_merchantRows")[0];
        Assert.That(SlotText(slot, "_draftAmountText"), Is.EqualTo("+3"));

        _ui.Present(View(potionDraft: 3, projectedCurrency: 20, canConfirm: false,
            new MerchantTradeBlock(MerchantTradeBlockReason.SubmissionInFlight)));
        Assert.That(SlotField<GameObject>(slot, "_draftBadge").activeSelf, Is.True);

        _ui.Present(View(potionDraft: 0, projectedCurrency: 100, canConfirm: false,
            new MerchantTradeBlock(MerchantTradeBlockReason.EmptyDraft)));
        Assert.That(SlotField<GameObject>(slot, "_draftBadge").activeSelf, Is.False);
        Assert.That(SlotText(slot, "_amountText"), Is.EqualTo("3"));
    }

    [Test]
    public void UnlimitedMerchantSlot_HidesItsStockButKeepsItsPrice()
    {
        var merchant = new List<MerchantShopRowViewModel>
        {
            new MerchantShopRowViewModel(_potion, Potion, true, 30, MerchantStockItem.UnlimitedQuantity, 0, 0, true,
                MerchantShopRowViewModel.UnlimitedDraftAmount)
        };
        _ui.Bind(_intentions);
        _ui.Present(new MerchantShopViewModel(true, 100, 100, 0, 0, 0, 2, 2, 20, false, false, false,
            new[] { new MerchantTradeBlock(MerchantTradeBlockReason.EmptyDraft) }, merchant, new List<MerchantShopRowViewModel>()));

        StoreItemUI slot = Rows("_merchantRows")[0];
        Assert.That(SlotText(slot, "_amountText"), Is.Empty);
        Assert.That(SlotText(slot, "_priceText"), Is.EqualTo("30"));
    }

    private MerchantShopViewModel View(
        int potionDraft,
        long projectedCurrency,
        bool canConfirm,
        params MerchantTradeBlock[] blocks) => View(potionDraft, projectedCurrency, canConfirm, 0, blocks);

    private MerchantShopViewModel View(
        int potionDraft,
        long projectedCurrency,
        bool canConfirm,
        int hatDraft,
        params MerchantTradeBlock[] blocks)
    {
        bool isInFlight = System.Array.Exists(blocks, block => block.Reason == MerchantTradeBlockReason.SubmissionInFlight);
        var merchant = new List<MerchantShopRowViewModel>
        {
            new MerchantShopRowViewModel(_potion, Potion, true, 30, 3, potionDraft, 30L * potionDraft, !isInFlight && potionDraft < 3, 3)
        };
        var inventory = new List<MerchantShopRowViewModel>
        {
            new MerchantShopRowViewModel(_hat, Hat, false, 5, 2, hatDraft, 5L * hatDraft, !isInFlight, 2)
        };

        return new MerchantShopViewModel(
            true, 100, projectedCurrency, 30L * potionDraft, 10, projectedCurrency - 100, 2, 3, 20,
            potionDraft > 0, isInFlight, canConfirm, blocks, merchant, inventory);
    }

    private void Select(LootId lootId, bool isMerchantStock) =>
        typeof(MerchantShopUI).GetMethod("OnItemSelected", PrivateInstance).Invoke(_ui, new object[] { lootId, isMerchantStock });

    private T Field<T>(string name) where T : class
    {
        FieldInfo field = typeof(MerchantShopUI).GetField(name, PrivateInstance);
        Assert.That(field, Is.Not.Null, name);
        return field.GetValue(_ui) as T;
    }

    private string Text(string name) => Field<TMP_Text>(name).text;

    private List<StoreItemUI> Rows(string name) => Field<List<StoreItemUI>>(name);

    private static GameObject Highlight(StoreItemUI row) => SlotField<GameObject>(row, "_selectionHighlight");

    private static T SlotField<T>(StoreItemUI slot, string name) where T : class
    {
        FieldInfo field = typeof(StoreItemUI).GetField(name, PrivateInstance);
        Assert.That(field, Is.Not.Null, name);
        return field.GetValue(slot) as T;
    }

    private static string SlotText(StoreItemUI slot, string name) => SlotField<TMP_Text>(slot, name).text;

    private sealed class RecordingIntentions : IMerchantShopIntentions
    {
        public bool AddPurchase(LootId lootId, int amount) => true;
        public bool SetPurchaseAmount(LootId lootId, int amount) => true;
        public bool RemovePurchase(LootId lootId) => true;
        public bool AddSale(LootId lootId, int amount) => true;
        public bool SetSaleAmount(LootId lootId, int amount) => true;
        public bool RemoveSale(LootId lootId) => true;
        public bool ClearTrade() => true;
        public bool ConfirmTrade() => true;
    }
}
