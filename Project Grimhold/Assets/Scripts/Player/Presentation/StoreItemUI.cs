using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One row of the merchant shop lists (merchant stock or player Inventory).
/// Designed to be attached to the root of the StoreItem Prefab. It renders an already priced row,
/// its draft amount and selection, and raises only its selection; lines are edited from the
/// shop's center panel.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoreItemUI : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _priceText;
    [SerializeField] private TMP_Text _stockText;

    [Header("Selection")]
    [SerializeField] private Button _selectButton;
    [SerializeField] private GameObject _selectionHighlight;

    private LootId _lootId;
    private bool _isMerchantStock;
    private Action<LootId, bool> _onSelected;

    public LootId LootId => _lootId;

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
    /// Renders one row. Merchant rows show the unreserved stock and the pending purchase amount;
    /// Inventory rows show the owned units and the pending sale amount.
    /// </summary>
    public void Present(MerchantShopRowViewModel row, bool isSelected, Action<LootId, bool> onSelected)
    {
        _lootId = row.LootId;
        _isMerchantStock = row.IsMerchantStock;
        _onSelected = onSelected;

        LootDefinition item = row.Definition;
        if (_iconImage != null) _iconImage.sprite = item != null ? item.Icon : null;
        if (_nameText != null) _nameText.text = item != null ? item.DisplayName : row.LootId.Value;
        if (_descriptionText != null) _descriptionText.text = item != null ? item.Description : string.Empty;
        if (_priceText != null) _priceText.text = row.UnitPrice.ToString();

        if (_stockText != null)
        {
            string available = row.IsUnlimited ? MerchantShopRowViewModel.UnlimitedDraftAmount.ToString() : row.Available.ToString();
            string pending = row.IsMerchantStock ? $" (+{row.DraftAmount})" : $" (-{row.DraftAmount})";
            _stockText.text = row.DraftAmount > 0 ? available + pending : available;
        }

        if (_selectionHighlight != null)
        {
            _selectionHighlight.SetActive(isSelected);
        }
    }
}
