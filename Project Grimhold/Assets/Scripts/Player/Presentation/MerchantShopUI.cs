using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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

    private TownMerchantNetworkController _merchantController;
    private ApplicationStashContext _context;
    
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
    }

    /// <summary>
    /// Initializes the UI with the necessary controllers and contexts.
    /// Called by the Presenter when opening the shop.
    /// </summary>
    public void Initialize(TownMerchantNetworkController merchantController, ApplicationStashContext context)
    {
        Debug.Log("[MerchantShopUI] Initialize called.");
        _merchantController = merchantController;
        _context = context;

        if (_merchantController != null)
        {
            _merchantController.LocalTransactionCompleted += HandleTransactionCompleted;
        }

        // Trigger initial refresh
        RefreshLists();
        OnNeedsVisualRefresh?.Invoke();
        Debug.Log("[MerchantShopUI] Initialize completed.");
    }

    /// <summary>
    /// Re-instantiates the lists. Useful when quantities change.
    /// </summary>
    public void RefreshLists()
    {
        if (_merchantController == null || _context == null) return;

        ClearContainer(_merchantStockContainer);
        ClearContainer(_playerInventoryContainer);

        PopulateMerchantStock();
        PopulatePlayerInventory();
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
                // By default we request purchase of 1 unit.
                instance.SetupForPurchase(stockItem.Item, remaining, id => RequestPurchase(id, 1));
            }
        }
    }

    private void PopulatePlayerInventory()
    {
        if (_playerInventoryContainer == null || _storeItemPrefab == null) return;

        var catalog = _merchantController.Catalog;
        var loadout = _context.Store.GetLoadout();

        foreach (var item in loadout)
        {
            if (catalog.TryGet(item.LootId.Value, out var definition))
            {
                var instance = Instantiate(_storeItemPrefab, _playerInventoryContainer);
                // By default we request sale of 1 unit.
                instance.SetupForSale(definition, item.Amount, id => RequestSale(id, 1));
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

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(RequestClose);
        }
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
        OnTransactionResult?.Invoke(result);

        // If the transaction was successful, the local store has been modified, so we should refresh visually
        if (result == MerchantTransactionResult.Success)
        {
            RefreshLists();
            OnNeedsVisualRefresh?.Invoke();
        }
    }
}
