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

    [SetUp]
    public void SetUp()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<MerchantShopUI>(MerchantShopPrefabPath);
        Assert.That(prefab, Is.Not.Null, MerchantShopPrefabPath);
        _ui = Object.Instantiate(prefab);
        _potion = MerchantTestContent.CreateDefinition(Potion.Value, buyValue: 30, sellValue: 20);
        _hat = MerchantTestContent.CreateDefinition(Hat.Value, buyValue: 10, sellValue: 5);
        _intentions = new RecordingIntentions();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_ui.gameObject);
        Object.DestroyImmediate(_potion);
        Object.DestroyImmediate(_hat);
    }

    [Test]
    public void ViewModelPresentedBeforeBind_IsRendered()
    {
        _ui.Present(View(potionDraft: 2, projectedCurrency: 50, canConfirm: true));
        _ui.Bind(_intentions);

        Assert.That(Text("_topCurrencyText"), Is.EqualTo("100 -> 50"));
        Assert.That(Text("_purchaseTotalText"), Is.EqualTo("Compra: 60"));
        Assert.That(Text("_saleTotalText"), Is.EqualTo("Venta: 10"));
        Assert.That(Text("_balanceText"), Is.EqualTo("Balance: -50"));
        Assert.That(Text("_capacityText"), Is.EqualTo("Espacio: 2/20 -> 3/20"));
        Assert.That(Text("_feedbackText"), Is.Empty);
        Assert.That(Field<Button>("_confirmButton").interactable, Is.True);
        Assert.That(Field<Button>("_clearButton").interactable, Is.True);
        Assert.That(Rows("_merchantRows").Count, Is.EqualTo(1));
        Assert.That(Rows("_inventoryRows").Count, Is.EqualTo(1));
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
        Assert.That(Text("_unitPriceText"), Is.EqualTo("30 c/u"));
        Assert.That(Text("_lineStateText"), Is.EqualTo("Compra agregada: 2 (60)"));
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

        Assert.That(Text("_lineStateText"), Is.EqualTo("Venta: sin agregar"));
        Assert.That(Text("_actionButtonText"), Is.EqualTo("Agregar"));
        Assert.That(Field<Button>("_actionButton").interactable, Is.True);
        Assert.That(Field<Button>("_removeLineButton").interactable, Is.False);
        Assert.That(Field<Button>("_clearButton").interactable, Is.False);
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

    private MerchantShopViewModel View(
        int potionDraft,
        long projectedCurrency,
        bool canConfirm,
        params MerchantTradeBlock[] blocks)
    {
        bool isInFlight = System.Array.Exists(blocks, block => block.Reason == MerchantTradeBlockReason.SubmissionInFlight);
        var merchant = new List<MerchantShopRowViewModel>
        {
            new MerchantShopRowViewModel(_potion, Potion, true, 30, 3, potionDraft, 30L * potionDraft, !isInFlight && potionDraft < 3, 3)
        };
        var inventory = new List<MerchantShopRowViewModel>
        {
            new MerchantShopRowViewModel(_hat, Hat, false, 5, 2, 0, 0, !isInFlight, 2)
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

    private static GameObject Highlight(StoreItemUI row) =>
        (GameObject)typeof(StoreItemUI).GetField("_selectionHighlight", PrivateInstance).GetValue(row);

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
