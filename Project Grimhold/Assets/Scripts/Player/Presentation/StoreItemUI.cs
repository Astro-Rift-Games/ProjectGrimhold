using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One compact grid slot of the merchant shop (merchant stock or player Inventory).
/// Designed to be attached to the root of the StoreItem Prefab. It renders an already priced row:
/// its icon, its confirmed amount, the unit price for merchant stock, a separate badge for the
/// amount placed in the draft, and its selection. It raises only its selection; name, description
/// and every other detail are shown, and lines are edited, from the shop's center panel.
/// An empty Inventory slot shows only its background and raises nothing.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoreItemUI : MonoBehaviour
{
    [SerializeField] private Image _iconImage;

    [Tooltip("Unreserved stock for merchant rows, confirmed owned units for Inventory rows.")]
    [FormerlySerializedAs("_stockText")]
    [SerializeField] private TMP_Text _amountText;

    [Header("Price (merchant stock only)")]
    [SerializeField] private GameObject _priceRoot;
    [SerializeField] private TMP_Text _priceText;

    [Header("Draft")]
    [SerializeField] private GameObject _draftBadge;
    [SerializeField] private Image _draftBadgeImage;
    [SerializeField] private TMP_Text _draftAmountText;
    [SerializeField] private Color _purchaseBadgeColor = new Color(0.16f, 0.42f, 0.2f);
    [SerializeField] private Color _saleBadgeColor = new Color(0.55f, 0.16f, 0.14f);

    [Header("Selection")]
    [SerializeField] private Button _selectButton;
    [SerializeField] private GameObject _selectionHighlight;

    private LootId _lootId;
    private bool _isMerchantStock;
    private Action<LootId, bool> _onSelected;

    public LootId LootId => _lootId;
    public bool IsEmpty { get; private set; }

    private void Awake()
    {
        if (_selectButton != null)
        {
            _selectButton.onClick.AddListener(OnSelectButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (_selectButton != null)
        {
            _selectButton.onClick.RemoveListener(OnSelectButtonClicked);
        }
    }

    private void OnSelectButtonClicked()
    {
        _onSelected?.Invoke(_lootId, _isMerchantStock);
    }

    /// <summary>
    /// Renders one slot. Merchant slots show the unit price, the unreserved stock when it is finite
    /// and a "+N" badge for the pending purchase; Inventory slots show the confirmed owned units and
    /// a "-N" badge for the pending sale. The badge never replaces the confirmed amount.
    /// </summary>
    public void Present(MerchantShopRowViewModel row, bool isSelected, Action<LootId, bool> onSelected)
    {
        IsEmpty = false;
        _lootId = row.LootId;
        _isMerchantStock = row.IsMerchantStock;
        _onSelected = onSelected;
        if (_selectButton != null) _selectButton.enabled = true;

        LootDefinition item = row.Definition;
        if (_iconImage != null)
        {
            _iconImage.enabled = true;
            _iconImage.sprite = item != null ? item.Icon : null;
        }

        if (_amountText != null) _amountText.text = row.IsUnlimited ? string.Empty : row.Available.ToString();

        if (_priceRoot != null) _priceRoot.SetActive(row.IsMerchantStock);
        if (_priceText != null) _priceText.text = row.UnitPrice.ToString();

        bool hasDraftLine = row.DraftAmount > 0;
        if (_draftBadge != null) _draftBadge.SetActive(hasDraftLine);
        if (_draftBadgeImage != null) _draftBadgeImage.color = row.IsMerchantStock ? _purchaseBadgeColor : _saleBadgeColor;
        if (_draftAmountText != null)
        {
            _draftAmountText.text = hasDraftLine ? (row.IsMerchantStock ? "+" : "-") + row.DraftAmount : string.Empty;
        }

        if (_selectionHighlight != null)
        {
            _selectionHighlight.SetActive(isSelected);
        }
    }

    /// <summary>
    /// Renders an unoccupied Inventory slot: only its background, with no selection, badge or
    /// intention. A disabled button receives no pointer events.
    /// </summary>
    public void PresentEmpty()
    {
        IsEmpty = true;
        _lootId = default;
        _isMerchantStock = false;
        _onSelected = null;

        if (_selectButton != null) _selectButton.enabled = false;
        if (_iconImage != null)
        {
            _iconImage.sprite = null;
            _iconImage.enabled = false;
        }

        if (_amountText != null) _amountText.text = string.Empty;
        if (_priceRoot != null) _priceRoot.SetActive(false);
        if (_draftBadge != null) _draftBadge.SetActive(false);
        if (_draftAmountText != null) _draftAmountText.text = string.Empty;
        if (_selectionHighlight != null) _selectionHighlight.SetActive(false);
    }
}
