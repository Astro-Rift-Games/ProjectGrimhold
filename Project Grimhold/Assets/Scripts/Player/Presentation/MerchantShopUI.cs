using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller for the Merchant Shop UI Prefab.
/// It exposes UnityEvents and public methods to allow the Unity Editor 
/// to bind UI elements without enforcing a specific framework.
/// Acts strictly as a viewer of LocalProfileStore and LootDefinitionCatalog.
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
    [SerializeField] private GameObject _centerPanelRoot; // Optional, to hide when nothing is selected
    [SerializeField] private Image _detailIcon;
    [SerializeField] private TMP_Text _detailName;
    [SerializeField] private TMP_Text _detailType;
    [SerializeField] private TMP_Text _detailRarity;
    [SerializeField] private TMP_Text _detailDescription;
    [SerializeField] private Transform _statsContainer;
    [SerializeField] private StatInfoUI _statInfoPrefab;

    [Header("Center Panel - Actions")]
    [SerializeField] private Slider _quantitySlider;
    [SerializeField] private TMP_Text _quantityText;
    [SerializeField] private Button _decreaseQtyButton;
    [SerializeField] private Button _increaseQtyButton;
    [SerializeField] private TMP_Text _totalPriceText;
    [SerializeField] private Button _actionButton;
    [SerializeField] private TMP_Text _actionButtonText;

    private TownMerchantNetworkController _merchantController;
    private ApplicationStashContext _context;
    private PlayerLootReceiver _playerLootReceiver;
    
    // State
    private LootDefinition _selectedItem;
    private bool _isSelectedFromMerchant;
    private int _selectedQuantity = 1;
    private int _maxAvailableQuantity = 1;
    private int _lastLootSequence = -1;
    private System.Collections.Generic.List<StoreItemUI> _instantiatedItems = new System.Collections.Generic.List<StoreItemUI>();
    
    [Header("Events")]
    [Tooltip("Fired when a transaction is fully processed and the UI should refresh its displays (e.g., currency, inventory).")]
    public UnityEvent OnNeedsVisualRefresh;
    
    [Tooltip("Fired to notify the player of a transaction result (e.g. Success, InsufficientFunds).")]
    public UnityEvent<MerchantTransactionResult> OnTransactionResult;

    [Tooltip("Fired when the user clicks the close button.")]
    public UnityEvent OnCloseRequested;

    private void Awake()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(RequestClose);
        }
        if (_quantitySlider != null)
        {
            _quantitySlider.onValueChanged.AddListener(OnSliderValueChanged);
        }
        if (_decreaseQtyButton != null)
        {
            _decreaseQtyButton.onClick.AddListener(OnDecreaseQuantityClicked);
        }
        if (_increaseQtyButton != null)
        {
            _increaseQtyButton.onClick.AddListener(OnIncreaseQuantityClicked);
        }
        if (_actionButton != null)
        {
            _actionButton.onClick.AddListener(OnCenterActionClicked);
        }
        
        ClearCenterPanel();
    }

    /// <summary>
    /// Initializes the UI with the necessary controllers and contexts.
    /// Called by the Presenter when opening the shop.
    /// </summary>
    public void Initialize(TownMerchantNetworkController merchantController, ApplicationStashContext context, PlayerLootReceiver playerLootReceiver)
    {
        Debug.Log("[MerchantShopUI] Initialize called.");
        _merchantController = merchantController;
        _context = context;
        _playerLootReceiver = playerLootReceiver;

        if (_playerLootReceiver != null)
        {
            _lastLootSequence = _playerLootReceiver.LootChangeSequence;
        }

        if (_merchantController != null)
        {
            _merchantController.LocalTransactionCompleted += HandleTransactionCompleted;
        }

        // Trigger initial refresh
        RefreshLists();
        OnNeedsVisualRefresh?.Invoke();
        Debug.Log("[MerchantShopUI] Initialize completed.");
    }

    private void Update()
    {
        if (_playerLootReceiver != null && _playerLootReceiver.LootChangeSequence != _lastLootSequence)
        {
            _lastLootSequence = _playerLootReceiver.LootChangeSequence;
            RefreshLists();
            OnNeedsVisualRefresh?.Invoke();
        }
    }

    private void UpdateCurrencyDisplay()
    {
        if (_topCurrencyText != null && _context != null)
        {
            _topCurrencyText.text = _context.Store.GetCurrency().ToString();
        }
    }

    /// <summary>
    /// Re-instantiates the lists. Useful when quantities change.
    /// </summary>
    public void RefreshLists()
    {
        if (_merchantController == null || _context == null) return;

        UpdateCurrencyDisplay();

        ClearContainer(_merchantStockContainer);
        ClearContainer(_playerInventoryContainer);
        _instantiatedItems.Clear();

        PopulateMerchantStock();
        PopulatePlayerInventory();
        
        // Re-validate selection after refresh
        if (_selectedItem != null)
        {
            // Re-select to update max quantities or clear if no longer available
            OnItemSelected(_selectedItem.Id, _isSelectedFromMerchant);
        }
    }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
    }

    private void PopulateMerchantStock()
    {
        if (_merchantStockContainer == null || _storeItemPrefab == null)
        {
            Debug.LogWarning("[MerchantShopUI] _merchantStockContainer or _storeItemPrefab is null.");
            return;
        }

        var stock = _merchantController.Stock;
        var catalog = _merchantController.Catalog;

        if (stock == null || catalog == null)
        {
            Debug.LogWarning($"[MerchantShopUI] Stock or Catalog is null. Stock: {stock != null}, Catalog: {catalog != null}");
            return;
        }

        Debug.Log($"[MerchantShopUI] Populating stock with {stock.Count} items.");
        foreach (var stockItem in stock)
        {
            if (stockItem.Item != null)
            {
                var instance = Instantiate(_storeItemPrefab, _merchantStockContainer);
                int remaining = _merchantController.GetRemainingStock(stockItem.Item.Id);
                instance.SetupForPurchase(stockItem.Item, remaining, id => RequestPurchase(id, 1), OnItemSelected);
                _instantiatedItems.Add(instance);
            }
        }
    }

    private void PopulatePlayerInventory()
    {
        if (_playerInventoryContainer == null || _storeItemPrefab == null || _playerLootReceiver == null) return;

        var catalog = _merchantController.Catalog;
        if (!_playerLootReceiver.TryGetLootContent(out var loadout)) return;

        foreach (var item in loadout)
        {
            if (catalog.TryGet(item.LootId.Value, out var definition))
            {
                var instance = Instantiate(_storeItemPrefab, _playerInventoryContainer);
                instance.SetupForSale(definition, item.Amount, id => RequestSale(id, 1), OnItemSelected);
                _instantiatedItems.Add(instance);
            }
        }
    }

    private void OnDisable()
    {
        if (_merchantController != null)
        {
            _merchantController.LocalTransactionCompleted -= HandleTransactionCompleted;
        }
    }
    
    private void OnDestroy()
    {
        if (_merchantController != null)
        {
            _merchantController.LocalTransactionCompleted -= HandleTransactionCompleted;
        }

        if (_closeButton != null) _closeButton.onClick.RemoveListener(RequestClose);
        if (_quantitySlider != null) _quantitySlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        if (_decreaseQtyButton != null) _decreaseQtyButton.onClick.RemoveListener(OnDecreaseQuantityClicked);
        if (_increaseQtyButton != null) _increaseQtyButton.onClick.RemoveListener(OnIncreaseQuantityClicked);
        if (_actionButton != null) _actionButton.onClick.RemoveListener(OnCenterActionClicked);
    }

    /// <summary>
    /// Requests a purchase via the Network Controller.
    /// To be wired in the Unity Editor to a "Buy" button.
    /// </summary>
    public void RequestPurchase(string lootIdRaw, int amount)
    {
        if (_merchantController == null) return;
        
        // Let the controller handle generating sequence and calling RPC
        _merchantController.RequestPurchase(new LootId(lootIdRaw), amount);
    }

    /// <summary>
    /// Requests a sale via the Network Controller.
    /// To be wired in the Unity Editor to a "Sell" button.
    /// </summary>
    public void RequestSale(string lootIdRaw, int amount)
    {
        Debug.Log($"[ShopTransaction] MerchantShopUI.RequestSale: LootId={lootIdRaw}, Amount={amount}");
        if (_merchantController == null) return;
        
        _merchantController.RequestSale(new LootId(lootIdRaw), amount);
    }

    /// <summary>
    /// Invokes the close requested event.
    /// </summary>
    private void RequestClose()
    {
        OnCloseRequested?.Invoke();
    }

    private void HandleTransactionCompleted(MerchantTransactionResult result)
    {
        Debug.Log($"[ShopTransaction] MerchantShopUI.HandleTransactionCompleted: Result={result}");
        OnTransactionResult?.Invoke(result);

        // If the transaction was successful, the local store has been modified, so we should refresh visually
        if (result == MerchantTransactionResult.Success)
        {
            RefreshLists();
            OnNeedsVisualRefresh?.Invoke();
        }
    }

    // --- Center Panel Logic ---

    private void OnItemSelected(string lootId, bool isMerchantStock)
    {
        if (_merchantController == null || _context == null) return;
        var catalog = _merchantController.Catalog;
        if (!catalog.TryGet(lootId, out LootDefinition definition)) return;

        _selectedItem = definition;
        _isSelectedFromMerchant = isMerchantStock;

        // Calculate max quantity available
        if (_isSelectedFromMerchant)
        {
            int remaining = _merchantController.GetRemainingStock(lootId);
            _maxAvailableQuantity = remaining == -1 ? 999 : remaining;
        }
        else
        {
            _maxAvailableQuantity = 0;
            if (_playerLootReceiver != null && _playerLootReceiver.TryGetLootContent(out var loadout))
            {
                foreach (var item in loadout)
                {
                    if (item.LootId.Value == lootId)
                    {
                        _maxAvailableQuantity = item.Amount;
                        break;
                    }
                }
            }
        }

        if (_maxAvailableQuantity <= 0)
        {
            ClearCenterPanel();
            return;
        }

        if (_centerPanelRoot != null) _centerPanelRoot.SetActive(true);

        // Update Details
        if (_detailIcon != null) _detailIcon.sprite = definition.Icon;
        if (_detailName != null) _detailName.text = definition.DisplayName;
        if (_detailType != null) _detailType.text = definition.Category.ToString();
        if (_detailRarity != null) _detailRarity.text = definition.Rarity.ToString();
        if (_detailDescription != null) _detailDescription.text = definition.Description;
        
        // Prepare stats container (currently no native stats exist in LootDefinition)
        ClearContainer(_statsContainer);
        // Example of how it will be populated later:
        // foreach (var stat in definition.GetStats()) 
        // { 
        //     var statUI = Instantiate(_statInfoPrefab, _statsContainer); 
        //     statUI.Setup(stat.Icon, stat.Name, stat.Value.ToString()); 
        // }

        // Setup Slider and Quantity
        _selectedQuantity = 1;
        if (_quantitySlider != null)
        {
            _quantitySlider.minValue = 1;
            _quantitySlider.maxValue = _maxAvailableQuantity;
            _quantitySlider.value = _selectedQuantity;
            _quantitySlider.interactable = _maxAvailableQuantity > 1;
        }
        
        UpdateQuantityVisuals();

        // Update Action Button
        if (_actionButtonText != null)
        {
            _actionButtonText.text = _isSelectedFromMerchant ? "Comprar" : "Vender";
        }
        if (_actionButton != null)
        {
            _actionButton.interactable = true;
        }
    }

    private void UpdateQuantityVisuals()
    {
        if (_selectedItem == null) return;

        if (_quantityText != null) _quantityText.text = _selectedQuantity.ToString();
        if (_decreaseQtyButton != null) _decreaseQtyButton.interactable = _selectedQuantity > 1;
        if (_increaseQtyButton != null) _increaseQtyButton.interactable = _selectedQuantity < _maxAvailableQuantity;

        long unitPrice = _isSelectedFromMerchant ? _selectedItem.ExtractionValuePerUnit : _selectedItem.SellValuePerUnit;
        long totalPrice = unitPrice * _selectedQuantity;

        if (_totalPriceText != null) _totalPriceText.text = totalPrice.ToString();
    }

    private void OnSliderValueChanged(float value)
    {
        _selectedQuantity = Mathf.RoundToInt(value);
        UpdateQuantityVisuals();
    }

    private void OnDecreaseQuantityClicked()
    {
        if (_selectedQuantity > 1)
        {
            _selectedQuantity--;
            if (_quantitySlider != null) _quantitySlider.value = _selectedQuantity;
            UpdateQuantityVisuals();
        }
    }

    private void OnIncreaseQuantityClicked()
    {
        if (_selectedQuantity < _maxAvailableQuantity)
        {
            _selectedQuantity++;
            if (_quantitySlider != null) _quantitySlider.value = _selectedQuantity;
            UpdateQuantityVisuals();
        }
    }

    private void OnCenterActionClicked()
    {
        if (_selectedItem == null || _selectedQuantity <= 0) return;

        Debug.Log($"[ShopTransaction] MerchantShopUI.OnCenterActionClicked: Merchant={_isSelectedFromMerchant}, Item={_selectedItem.Id}, Qty={_selectedQuantity}");

        if (_isSelectedFromMerchant)
        {
            RequestPurchase(_selectedItem.Id, _selectedQuantity);
        }
        else
        {
            RequestSale(_selectedItem.Id, _selectedQuantity);
        }
    }

    private void ClearCenterPanel()
    {
        _selectedItem = null;
        if (_centerPanelRoot != null) _centerPanelRoot.SetActive(false);
    }
}
