using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller for a single item element in the merchant shop lists (either buying or selling).
/// Designed to be attached to the root of the StoreItem Prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoreItemUI : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _priceText;
    [SerializeField] private TMP_Text _stockText;
    [SerializeField] private Button _actionButton;
    [SerializeField] private TMP_Text _actionButtonText;
    
    [Header("Selection")]
    [SerializeField] private Button _selectButton;
    [SerializeField] private GameObject _selectionHighlight;

    private string _currentLootId;
    private Action<string> _onFastActionClicked;
    private Action<string, bool> _onSelected;
    private bool _isMerchantStock;

    private void Awake()
    {
        if (_actionButton != null)
        {
            if (_actionButton != null)
            {
                _actionButton.onClick.AddListener(OnFastActionButtonClicked);
            }
            if (_selectButton != null)
            {
                _selectButton.onClick.AddListener(OnSelectButtonClicked);
            }
        }
    }

    private void OnDestroy()
    {
        if (_actionButton != null)
        {
            _actionButton.onClick.RemoveListener(OnFastActionButtonClicked);
        }
        if (_selectButton != null)
        {
            _selectButton.onClick.RemoveListener(OnSelectButtonClicked);
        }
    }

    private void OnFastActionButtonClicked()
    {
        _onFastActionClicked?.Invoke(_currentLootId);
    }
    
    private void OnSelectButtonClicked()
    {
        _onSelected?.Invoke(_currentLootId, _isMerchantStock);
    }
    
    public void SetSelected(bool isSelected)
    {
        if (_selectionHighlight != null)
        {
            _selectionHighlight.SetActive(isSelected);
        }
    }

    /// <summary>
    /// Configures the UI element for purchasing an item from the merchant.
    /// </summary>
    public void SetupForPurchase(LootDefinition item, int remainingStock, Action<string> onFastBuy, Action<string, bool> onSelected)
    {
        _currentLootId = item.Id;
        _isMerchantStock = true;
        _onFastActionClicked = onFastBuy;
        _onSelected = onSelected;
        SetSelected(false);

        if (_iconImage != null) _iconImage.sprite = item.Icon;
        if (_nameText != null) _nameText.text = item.DisplayName;
        if (_descriptionText != null) _descriptionText.text = item.Description;
        if (_priceText != null) _priceText.text = item.ExtractionValuePerUnit.ToString();

        bool isUnlimited = remainingStock == -1;
        if (_stockText != null)
        {
            _stockText.text = isUnlimited ? "999" : remainingStock.ToString();
        }

        if (_actionButton != null)
        {
            bool canBuy = isUnlimited || remainingStock > 0;
            _actionButton.interactable = canBuy;
            
            if (_actionButtonText != null)
            {
                _actionButtonText.text = canBuy ? "Comprar" : "Agotado";
            }
        }
    }

    /// <summary>
    /// Configures the UI element for selling an item from the player's inventory.
    /// </summary>
    public void SetupForSale(LootDefinition item, int quantityOwned, Action<string> onFastSell, Action<string, bool> onSelected)
    {
        _currentLootId = item.Id;
        _isMerchantStock = false;
        _onFastActionClicked = onFastSell;
        _onSelected = onSelected;
        SetSelected(false);

        if (_iconImage != null) _iconImage.sprite = item.Icon;
        if (_nameText != null) _nameText.text = item.DisplayName;
        if (_descriptionText != null) _descriptionText.text = item.Description;
        if (_priceText != null) _priceText.text = item.SellValuePerUnit.ToString();

        if (_stockText != null)
        {
            _stockText.text = quantityOwned.ToString();
        }

        if (_actionButton != null)
        {
            _actionButton.interactable = quantityOwned > 0;
            
            if (_actionButtonText != null)
            {
                _actionButtonText.text = "Vender";
            }
        }
    }
}
