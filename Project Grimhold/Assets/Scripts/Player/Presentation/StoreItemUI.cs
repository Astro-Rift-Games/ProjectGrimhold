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

    private string _currentLootId;
    private Action<string> _onActionClicked;

    private void Awake()
    {
        if (_actionButton != null)
        {
            _actionButton.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (_actionButton != null)
        {
            _actionButton.onClick.RemoveListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        _onActionClicked?.Invoke(_currentLootId);
    }

    /// <summary>
    /// Configures the UI element for purchasing an item from the merchant.
    /// </summary>
    public void SetupForPurchase(LootDefinition item, int remainingStock, Action<string> onBuy)
    {
        _currentLootId = item.Id;
        _onActionClicked = onBuy;

        if (_iconImage != null) _iconImage.sprite = item.Icon;
        if (_nameText != null) _nameText.text = item.DisplayName;
        if (_descriptionText != null) _descriptionText.text = item.Description;
        if (_priceText != null) _priceText.text = $"{item.ExtractionValuePerUnit} Oro";

        bool isUnlimited = remainingStock == -1;
        if (_stockText != null)
        {
            _stockText.text = isUnlimited ? "Stock: Infinito" : $"Stock: {remainingStock}";
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
    public void SetupForSale(LootDefinition item, int quantityOwned, Action<string> onSell)
    {
        _currentLootId = item.Id;
        _onActionClicked = onSell;

        if (_iconImage != null) _iconImage.sprite = item.Icon;
        if (_nameText != null) _nameText.text = item.DisplayName;
        if (_descriptionText != null) _descriptionText.text = item.Description;
        if (_priceText != null) _priceText.text = $"{item.SellValuePerUnit} Oro";

        if (_stockText != null)
        {
            _stockText.text = $"Tienes: {quantityOwned}";
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
