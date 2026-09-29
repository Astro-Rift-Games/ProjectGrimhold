using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Presentation of the Merchant Shop Prefab. It renders the <see cref="MerchantShopViewModel"/>
/// given by <see cref="TownMerchantPresenter"/> through one flow: select a row, choose a quantity,
/// add or update its line, review the trade, then confirm or clear it. Selection and quantity live
/// in <see cref="MerchantShopInteraction"/>; this component only binds widgets to it. It holds no
/// services, network controller or economy rules.
/// </summary>
[DisallowMultipleComponent]
public sealed class MerchantShopUI : MonoBehaviour
{
    [Header("Dynamic UI References")]
    [SerializeField] private StoreItemUI _storeItemPrefab;
    [SerializeField] private Transform _merchantStockContainer;
    [SerializeField] private Transform _playerInventoryContainer;
    [SerializeField] private Button _closeButton;

    [Header("Top Panel")]
    [SerializeField] private TMP_Text _topCurrencyText;

    [Header("Center Panel - Details")]
    [SerializeField] private GameObject _centerPanelRoot; // Hidden while nothing is selected
    [SerializeField] private Image _detailIcon;
    [SerializeField] private TMP_Text _detailName;
    [SerializeField] private TMP_Text _detailType;
    [SerializeField] private TMP_Text _detailRarity;
    [SerializeField] private TMP_Text _detailDescription;
    [SerializeField] private TMP_Text _detailPropertiesText;

    [Header("Center Panel - Line")]
    [SerializeField] private Slider _quantitySlider;
    [SerializeField] private TMP_Text _quantityText;
    [SerializeField] private Button _decreaseQtyButton;
    [SerializeField] private Button _increaseQtyButton;
    [SerializeField] private TMP_Text _unitPriceText;
    [SerializeField] private TMP_Text _lineStateText;
    [SerializeField] private Button _actionButton;
    [SerializeField] private TMP_Text _actionButtonText;
    [SerializeField] private Button _removeLineButton;

    [Header("Trade Summary")]
    [SerializeField] private TMP_Text _purchaseTotalText;
    [SerializeField] private TMP_Text _saleTotalText;
    [SerializeField] private TMP_Text _balanceText;
    [SerializeField] private TMP_Text _capacityText;
    [SerializeField] private TMP_Text _feedbackText;

    [Header("Trade")]
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _clearButton;

    // The shop font has no arrow glyph.
    private const string Projection = " -> ";

    private readonly List<StoreItemUI> _merchantRows = new List<StoreItemUI>();
    private readonly List<StoreItemUI> _inventoryRows = new List<StoreItemUI>();
    private MerchantShopInteraction _interaction;
    private MerchantShopViewModel _viewModel = MerchantShopViewModel.Unavailable;
    private string _resultMessage = string.Empty;

    [Header("Events")]
    [Tooltip("Fired to notify the player of a trade result (e.g. Success, RejectedByProfile).")]
    public UnityEvent<MerchantTransactionResult> OnTransactionResult;

    [Tooltip("Fired when the user clicks the close button.")]
    public UnityEvent OnCloseRequested;

    private void Awake()
    {
        if (_closeButton != null) _closeButton.onClick.AddListener(RequestClose);
        if (_quantitySlider != null) _quantitySlider.onValueChanged.AddListener(OnSliderValueChanged);
        if (_decreaseQtyButton != null) _decreaseQtyButton.onClick.AddListener(OnDecreaseQuantityClicked);
        if (_increaseQtyButton != null) _increaseQtyButton.onClick.AddListener(OnIncreaseQuantityClicked);
        if (_actionButton != null) _actionButton.onClick.AddListener(OnApplyLineClicked);
        if (_removeLineButton != null) _removeLineButton.onClick.AddListener(OnRemoveLineClicked);
        if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
        if (_clearButton != null) _clearButton.onClick.AddListener(OnClearClicked);

        Render();
    }

    private void OnDestroy()
    {
        if (_closeButton != null) _closeButton.onClick.RemoveListener(RequestClose);
        if (_quantitySlider != null) _quantitySlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        if (_decreaseQtyButton != null) _decreaseQtyButton.onClick.RemoveListener(OnDecreaseQuantityClicked);
        if (_increaseQtyButton != null) _increaseQtyButton.onClick.RemoveListener(OnIncreaseQuantityClicked);
        if (_actionButton != null) _actionButton.onClick.RemoveListener(OnApplyLineClicked);
        if (_removeLineButton != null) _removeLineButton.onClick.RemoveListener(OnRemoveLineClicked);
        if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (_clearButton != null) _clearButton.onClick.RemoveListener(OnClearClicked);
    }

    /// <summary>
    /// Connects the trade intentions of the session being shown. A session presents its first
    /// view model while it is created, before binding, so the latest one is applied here.
    /// </summary>
    public void Bind(IMerchantShopIntentions intentions)
    {
        _interaction = intentions != null ? new MerchantShopInteraction(intentions) : null;
        _resultMessage = string.Empty;
        _interaction?.Present(_viewModel);
        Render();
    }

    public void Unbind()
    {
        _interaction = null;
        _viewModel = MerchantShopViewModel.Unavailable;
        _resultMessage = string.Empty;
        ClearRows(_merchantRows);
        ClearRows(_inventoryRows);
        Render();
    }

    public void Present(MerchantShopViewModel viewModel)
    {
        _viewModel = viewModel ?? MerchantShopViewModel.Unavailable;
        if (_interaction == null)
        {
            return;
        }

        _interaction.Present(_viewModel);
        Render();
    }

    public void PresentResult(MerchantTransactionResult result)
    {
        _resultMessage = MerchantTradeFeedback.DescribeResult(result);
        RenderSummary();
        OnTransactionResult?.Invoke(result);
    }

    private void Render()
    {
        if (_interaction != null)
        {
            PresentRows(_merchantStockContainer, _merchantRows, _interaction.ViewModel.MerchantRows);
            PresentRows(_playerInventoryContainer, _inventoryRows, _interaction.ViewModel.InventoryRows);
        }

        RenderSummary();
        RenderSelection();
    }

    private void PresentRows(Transform container, List<StoreItemUI> instances, IReadOnlyList<MerchantShopRowViewModel> rows)
    {
        if (container == null || _storeItemPrefab == null)
        {
            return;
        }

        if (!HaveSameRows(instances, rows))
        {
            ClearRows(instances);
            for (int i = 0; i < rows.Count; i++)
            {
                instances.Add(Instantiate(_storeItemPrefab, container));
            }
        }

        for (int i = 0; i < rows.Count; i++)
        {
            instances[i].Present(rows[i], _interaction.IsSelected(rows[i]), OnItemSelected);
        }
    }

    private static bool HaveSameRows(List<StoreItemUI> instances, IReadOnlyList<MerchantShopRowViewModel> rows)
    {
        if (instances.Count != rows.Count)
        {
            return false;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (instances[i] == null || instances[i].LootId != rows[i].LootId)
            {
                return false;
            }
        }

        return true;
    }

    private static void ClearRows(List<StoreItemUI> instances)
    {
        foreach (StoreItemUI instance in instances)
        {
            if (instance != null)
            {
                Destroy(instance.gameObject);
            }
        }

        instances.Clear();
    }

    private void RenderSummary()
    {
        MerchantShopViewModel viewModel = _interaction?.ViewModel ?? MerchantShopViewModel.Unavailable;

        SetText(_topCurrencyText, viewModel.HasDraft
            ? viewModel.ConfirmedCurrency + Projection + viewModel.ProjectedCurrency
            : viewModel.ConfirmedCurrency.ToString());
        SetText(_purchaseTotalText, $"Compra: {viewModel.PurchaseTotal}");
        SetText(_saleTotalText, $"Venta: {viewModel.SaleTotal}");
        SetText(_balanceText, $"Balance: {viewModel.Balance.ToString("+#;-#;0")}");
        SetText(_capacityText,
            $"Espacio: {viewModel.OccupiedSlots}/{viewModel.SlotCapacity}{Projection}{viewModel.ProjectedOccupiedSlots}/{viewModel.SlotCapacity}");

        // A trade outcome stays visible until the next edit; an in-flight request always wins.
        SetText(_feedbackText, !viewModel.IsInFlight && !string.IsNullOrEmpty(_resultMessage)
            ? _resultMessage
            : MerchantTradeFeedback.DescribePrimaryBlock(viewModel));

        if (_confirmButton != null) _confirmButton.interactable = _interaction != null && _interaction.CanConfirm;
        if (_clearButton != null) _clearButton.interactable = _interaction != null && _interaction.CanClear;
    }

    private void RenderSelection()
    {
        if (_interaction == null || !_interaction.HasSelection)
        {
            if (_centerPanelRoot != null) _centerPanelRoot.SetActive(false);
            return;
        }

        if (_centerPanelRoot != null) _centerPanelRoot.SetActive(true);

        MerchantShopRowViewModel row = _interaction.SelectedRow;
        LootDefinition definition = row.Definition;
        EquipmentTooltipPresentation information = EquipmentTooltipPresentationBuilder.Build(definition);

        if (_detailIcon != null) _detailIcon.sprite = definition != null ? definition.Icon : null;
        SetText(_detailName, information.CanShow ? information.Title : row.LootId.Value);
        SetText(_detailType, definition != null ? definition.Category.ToString() : string.Empty);
        SetText(_detailRarity, definition != null ? definition.Rarity.ToString() : string.Empty);
        SetText(_detailDescription, definition != null ? definition.Description : string.Empty);
        SetText(_detailPropertiesText, information.Body);

        if (_quantitySlider != null)
        {
            _quantitySlider.minValue = 1;
            _quantitySlider.maxValue = _interaction.MaxQuantity;
            _quantitySlider.SetValueWithoutNotify(_interaction.Quantity);
            _quantitySlider.interactable = _interaction.CanChangeQuantity;
        }

        SetText(_quantityText, _interaction.Quantity.ToString());
        if (_decreaseQtyButton != null) _decreaseQtyButton.interactable = _interaction.CanDecreaseQuantity;
        if (_increaseQtyButton != null) _increaseQtyButton.interactable = _interaction.CanIncreaseQuantity;

        SetText(_unitPriceText, $"{row.UnitPrice} c/u");
        SetText(_lineStateText, DescribeLine(row));
        SetText(_actionButtonText, _interaction.LineAction == MerchantShopLineAction.Update ? "Actualizar" : "Agregar");
        if (_actionButton != null) _actionButton.interactable = _interaction.CanApplyLine;
        if (_removeLineButton != null) _removeLineButton.interactable = _interaction.CanRemoveLine;
    }

    private static string DescribeLine(MerchantShopRowViewModel row)
    {
        string side = row.IsMerchantStock ? "Compra" : "Venta";
        return row.DraftAmount > 0
            ? $"{side} agregada: {row.DraftAmount} ({row.DraftTotal})"
            : $"{side}: sin agregar";
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private void RequestClose()
    {
        OnCloseRequested?.Invoke();
    }

    private void OnItemSelected(LootId lootId, bool isMerchantStock)
    {
        if (_interaction != null && _interaction.Select(lootId, isMerchantStock))
        {
            Render();
        }
    }

    private void OnSliderValueChanged(float value)
    {
        if (_interaction != null && _interaction.SetQuantity(Mathf.RoundToInt(value)))
        {
            RenderSelection();
        }
    }

    private void OnDecreaseQuantityClicked()
    {
        if (_interaction != null && _interaction.DecreaseQuantity())
        {
            RenderSelection();
        }
    }

    private void OnIncreaseQuantityClicked()
    {
        if (_interaction != null && _interaction.IncreaseQuantity())
        {
            RenderSelection();
        }
    }

    // Edits re-render through Present, which the session calls synchronously on every change.

    private void OnApplyLineClicked()
    {
        _resultMessage = string.Empty;
        _interaction?.ApplyLine();
    }

    private void OnRemoveLineClicked()
    {
        _resultMessage = string.Empty;
        _interaction?.RemoveLine();
    }

    private void OnClearClicked()
    {
        _resultMessage = string.Empty;
        _interaction?.Clear();
    }

    private void OnConfirmClicked()
    {
        if (_interaction == null)
        {
            return;
        }

        _resultMessage = string.Empty;
        _interaction.Confirm();
        RenderSummary();
    }
}
