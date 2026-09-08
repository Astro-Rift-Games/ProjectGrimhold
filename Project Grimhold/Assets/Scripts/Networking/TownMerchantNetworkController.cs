using Fusion;
using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Handles the network protocol for shop transactions.
/// Runs validation on StateAuthority (Master Client) and routes 
/// responses to the local orchestrator on the requesting client.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class TownMerchantNetworkController : NetworkBehaviour, IMasterClientRpcSender
{
    [SerializeField] private LootDefinitionCatalog _catalog;
    [SerializeField] private List<MerchantStockItem> _stock;

    public IReadOnlyList<MerchantStockItem> Stock => _stock;
    public LootDefinitionCatalog Catalog => _catalog;

    private MerchantRequestValidator _requestValidator;
    private MerchantTransactionOrchestrator _localOrchestrator;
    private IShopTransactionService _shopService;
    private ProfileId _localProfileId;
    
    // Local tracking to disable UI buttons
    private Dictionary<string, int> _mySessionPurchases = new Dictionary<string, int>();

    public event Action<MerchantTransactionResult> LocalTransactionCompleted;

    public void InitializeLocalClient(IShopTransactionService shopService, ProfileId profileId)
    {
        ReleaseLocalOrchestrator();
        _shopService = shopService;
        _localProfileId = profileId;
        _localOrchestrator = new MerchantTransactionOrchestrator(_shopService, _catalog, this, _localProfileId);
        _localOrchestrator.TransactionCompleted += OnLocalTransactionCompleted;
        _localOrchestrator.LocalPurchaseSucceeded += RecordLocalPurchase;
    }

    private void OnLocalTransactionCompleted(MerchantTransactionResult result)
    {
        LocalTransactionCompleted?.Invoke(result);
    }
    
    /// <summary>
    /// Intended for the local UI to know if the player can still buy this item.
    /// </summary>
    public int GetRemainingStock(string lootId)
    {
        if (_stock == null) return 0;
        foreach (var item in _stock)
        {
            if (item.Item != null && item.Item.Id == lootId)
            {
                if (item.MaxQuantity == -1) return -1; // Unlimited
                int purchased = 0;
                _mySessionPurchases.TryGetValue(lootId, out purchased);
                return Mathf.Max(0, item.MaxQuantity - purchased);
            }
        }
        return 0; // Not sold here
    }
    
    /// <summary>
    /// Local UI calls this upon success, or the orchestrator does it.
    /// </summary>
    public void RecordLocalPurchase(string lootId, int amount)
    {
        if (!_mySessionPurchases.ContainsKey(lootId))
        {
            _mySessionPurchases[lootId] = 0;
        }
        _mySessionPurchases[lootId] += amount;
    }

    public override void Spawned()
    {
        _requestValidator = new MerchantRequestValidator(_stock);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        ReleaseLocalOrchestrator();
    }

    public void RequestPurchase(LootId lootId, int amount)
    {
        _localOrchestrator?.RequestPurchase(lootId, amount);
    }

    public void RequestSale(LootId lootId, int amount)
    {
        Debug.Log($"[ShopTransaction] TownMerchantNetworkController.RequestSale: LootId={lootId.Value}, Amount={amount}");
        _localOrchestrator?.RequestSale(lootId, amount);
    }

    // --- IMasterClientRpcSender ---
    void IMasterClientRpcSender.SendPurchaseRequest(LootId lootId, int amount, int clientSequence)
    {
        Rpc_RequestPurchase(lootId.Value, amount, clientSequence);
    }

    void IMasterClientRpcSender.SendSaleRequest(LootId lootId, int amount, int clientSequence)
    {
        Rpc_RequestSale(lootId.Value, amount, clientSequence);
    }

    // --- RPCs ---
    [Rpc(RpcSources.All, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void Rpc_RequestPurchase(string lootId, int amount, int clientSequence, RpcInfo info = default)
    {
        PlayerRef source = info.Source.IsNone ? Runner.LocalPlayer : info.Source;
        Debug.Log($"[ShopTransaction] TownMerchantNetworkController.Rpc_RequestPurchase (Server): LootId={lootId}, Amount={amount}, Seq={clientSequence}, Source={source}");
        
        if (_requestValidator == null) return;
        
        if (_requestValidator.TryProcessPurchaseRequest(
                source,
                clientSequence,
                lootId,
                amount,
                _catalog,
                out bool isApproved,
                out ShopTransactionId txId))
        {
            Rpc_PurchaseResponse(source, clientSequence, isApproved, txId.Timestamp, txId.Value);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void Rpc_RequestSale(string lootId, int amount, int clientSequence, RpcInfo info = default)
    {
        PlayerRef source = info.Source.IsNone ? Runner.LocalPlayer : info.Source;
        Debug.Log($"[ShopTransaction] TownMerchantNetworkController.Rpc_RequestSale (Server): LootId={lootId}, Amount={amount}, Seq={clientSequence}, Source={source}");
        
        if (_requestValidator == null) return;
        
        if (_requestValidator.TryProcessSaleRequest(
                source,
                clientSequence,
                lootId,
                amount,
                _catalog,
                out bool isApproved,
                out ShopTransactionId txId))
        {
            Debug.Log($"[ShopTransaction] Server: TryProcessSaleRequest processed. isApproved={isApproved}, txId={txId.Timestamp}");
            Rpc_SaleResponse(source, clientSequence, isApproved, txId.Timestamp, txId.Value);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void Rpc_PurchaseResponse([RpcTarget] PlayerRef target, int clientSequence, bool isApproved, long timestamp, Guid txGuid)
    {
        var txId = isApproved ? new ShopTransactionId(timestamp, txGuid) : default;
        _localOrchestrator?.OnPurchaseResponseReceived(clientSequence, isApproved, txId);
    }

    [Rpc(RpcSources.All, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void Rpc_SaleResponse([RpcTarget] PlayerRef target, int clientSequence, bool isApproved, long timestamp, Guid txGuid)
    {
        Debug.Log($"[ShopTransaction] TownMerchantNetworkController.Rpc_SaleResponse (Client): Seq={clientSequence}, isApproved={isApproved}");
        var txId = isApproved ? new ShopTransactionId(timestamp, txGuid) : default;
        _localOrchestrator?.OnSaleResponseReceived(clientSequence, isApproved, txId);
    }

    private void ReleaseLocalOrchestrator()
    {
        if (_localOrchestrator == null)
        {
            return;
        }

        _localOrchestrator.TransactionCompleted -= OnLocalTransactionCompleted;
        _localOrchestrator.LocalPurchaseSucceeded -= RecordLocalPurchase;
        _localOrchestrator = null;
    }
}
