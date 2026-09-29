using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Assert = NUnit.Framework.Assert;

public class MerchantShopUIBoundaryTests
{
    private const string MerchantShopPrefabPath = "Assets/Prefabs/UI/MerchantShopUI.prefab";
    private const string StoreItemPrefabPath = "Assets/Prefabs/UI/StoreItem.prefab";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly Type[] ForbiddenDependencies =
    {
        typeof(ApplicationStashContext),
        typeof(IPlayerLoadoutService),
        typeof(IPlayerCurrencyService),
        typeof(IShopTransactionService),
        typeof(LocalProfileStore),
        typeof(TownMerchantNetworkController),
        typeof(IMerchantTradeEndpoint),
        typeof(LootDefinitionCatalog),
        typeof(MerchantTradeDraft),
        typeof(TownMerchantTradeSession)
    };

    [TestCase(typeof(MerchantShopUI))]
    [TestCase(typeof(StoreItemUI))]
    [TestCase(typeof(MerchantShopInteraction))]
    public void Ui_HoldsNoServicesControllerOrTradeState(Type uiType)
    {
        foreach (FieldInfo field in uiType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (Type forbidden in ForbiddenDependencies)
            {
                Assert.That(forbidden.IsAssignableFrom(field.FieldType), Is.False, $"{uiType.Name}.{field.Name} is a {forbidden.Name}.");
            }
        }
    }

    [TestCase("Assets/Scripts/Player/Presentation/MerchantShopUI.cs")]
    [TestCase("Assets/Scripts/Player/Presentation/StoreItemUI.cs")]
    [TestCase("Assets/Scripts/Player/Presentation/MerchantShopInteraction.cs")]
    [TestCase("Assets/Scripts/Player/Presentation/MerchantTradeFeedback.cs")]
    public void Ui_ComputesNoEconomyAndLocatesNoServices(string sourcePath)
    {
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Not.Contain("BuyValuePerUnit"));
        Assert.That(source, Does.Not.Contain("SellValuePerUnit"));
        Assert.That(source, Does.Not.Contain("TryExecuteTrade"));
        Assert.That(source, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(source, Does.Not.Contain("MerchantTradePreview"), "The UI reads the preview only through the view model.");
        Assert.That(source, Does.Not.Contain("UnitPrice *"), "The UI multiplies no prices.");
        Assert.That(source, Does.Not.Contain("* row.UnitPrice"), "The UI multiplies no prices.");
    }

    [Test]
    public void MerchantShopUi_EnablesConfirmOnlyThroughTheViewModel()
    {
        string interaction = File.ReadAllText("Assets/Scripts/Player/Presentation/MerchantShopInteraction.cs");
        string ui = File.ReadAllText("Assets/Scripts/Player/Presentation/MerchantShopUI.cs");

        Assert.That(interaction, Does.Contain("public bool CanConfirm => ViewModel.CanConfirm && !_confirmPending;"));
        Assert.That(ui, Does.Contain("_confirmButton.interactable = _interaction != null && _interaction.CanConfirm;"));
    }

    [Test]
    public void StoreItem_HasNoQuickBuyOrSell()
    {
        Assert.That(typeof(StoreItemUI).GetField("_actionButton", PrivateInstance), Is.Null);
        Assert.That(typeof(StoreItemUI).GetField("_actionButtonText", PrivateInstance), Is.Null);

        MethodInfo present = typeof(StoreItemUI).GetMethod(nameof(StoreItemUI.Present));
        Assert.That(present.GetParameters().Select(parameter => parameter.ParameterType),
            Is.EqualTo(new[] { typeof(MerchantShopRowViewModel), typeof(bool), typeof(Action<LootId, bool>) }),
            "A row only raises its selection.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StoreItemPrefabPath);
        Assert.That(prefab, Is.Not.Null, StoreItemPrefabPath);
        Button[] buttons = prefab.GetComponentsInChildren<Button>(true);
        Assert.That(buttons, Has.Length.EqualTo(1), "Only the row selection button remains.");
        Assert.That(GetField<Button>(prefab.GetComponent<StoreItemUI>(), "_selectButton"), Is.SameAs(buttons[0]));
    }

    [Test]
    public void MerchantShopUi_HasNoQuickBuyOrSellHandlers()
    {
        string source = File.ReadAllText("Assets/Scripts/Player/Presentation/MerchantShopUI.cs");

        Assert.That(source, Does.Not.Contain("QuickAdd"));
        Assert.That(source, Does.Not.Contain("AddPurchase("), "Lines are added only through the interaction.");
        Assert.That(source, Does.Not.Contain("AddSale("), "Lines are added only through the interaction.");
    }

    [Test]
    public void Presenter_ReceivesTheProfileThroughExplicitComposition()
    {
        string source = File.ReadAllText("Assets/Scripts/Player/Presentation/TownMerchantPresenter.cs");

        Assert.That(source, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(source, Does.Not.Contain("ApplicationStashContext"));
    }

    [TestCase("_confirmButton")]
    [TestCase("_clearButton")]
    [TestCase("_closeButton")]
    [TestCase("_actionButton")]
    [TestCase("_removeLineButton")]
    [TestCase("_decreaseQtyButton")]
    [TestCase("_increaseQtyButton")]
    [TestCase("_quantitySlider")]
    [TestCase("_quantityText")]
    [TestCase("_actionButtonText")]
    [TestCase("_unitPriceText")]
    [TestCase("_lineStateText")]
    [TestCase("_detailPropertiesText")]
    [TestCase("_topCurrencyText")]
    [TestCase("_purchaseTotalText")]
    [TestCase("_saleTotalText")]
    [TestCase("_balanceText")]
    [TestCase("_capacityText")]
    [TestCase("_feedbackText")]
    public void Prefab_WiresTradeFlowControls(string fieldName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MerchantShopPrefabPath);
        Assert.That(prefab, Is.Not.Null, MerchantShopPrefabPath);
        MerchantShopUI ui = prefab.GetComponent<MerchantShopUI>();
        Assert.That(ui, Is.Not.Null);

        var control = GetField<Component>(ui, fieldName);
        Assert.That(control, Is.Not.Null, fieldName);
        Assert.That(control.transform.IsChildOf(prefab.transform), Is.True, fieldName);
    }

    [Test]
    public void Prefab_KeepsTradeSummaryVisibleWithoutSelection()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MerchantShopPrefabPath);
        MerchantShopUI ui = prefab.GetComponent<MerchantShopUI>();
        var centerPanel = GetField<GameObject>(ui, "_centerPanelRoot");

        foreach (string fieldName in new[] { "_purchaseTotalText", "_saleTotalText", "_balanceText", "_capacityText", "_feedbackText", "_confirmButton", "_clearButton" })
        {
            Assert.That(GetField<Component>(ui, fieldName).transform.IsChildOf(centerPanel.transform), Is.False, fieldName);
        }
    }

    private static T GetField<T>(object target, string fieldName) where T : class
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, fieldName);
        return field.GetValue(target) as T;
    }

    [Test]
    public void SocialPlayer_ComposesTheMerchantPresenterFromTheTownProfileBinder()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SocialPlayer.prefab");
        TownInventoryBinder binder = prefab.GetComponentsInChildren<TownInventoryBinder>(true).Single();

        var presenter = (TownMerchantPresenter)typeof(TownInventoryBinder)
            .GetField("_merchantPresenter", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(binder);

        Assert.That(presenter, Is.Not.Null);
        Assert.That(presenter.gameObject, Is.SameAs(binder.gameObject));
    }
}
