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
    private class PlayerLootReceiverHandler : IMerchantInventoryHandler
    {
        private readonly PlayerLootReceiver _receiver;
        private readonly EntityId _merchantId;

        public PlayerLootReceiverHandler(PlayerLootReceiver receiver, EntityId merchantId)
        {
            _receiver = receiver;
            _merchantId = merchantId;
        }

        public bool ValidatePurchase(string lootId, int amount)
        {
            if (_receiver == null) return false;
            var req = new LootTransferRequest(_merchantId, _receiver.Id, new LootId(lootId), amount, 0);
            return _receiver.ValidateReceive(req) == LootTransferFailureReason.None;
        }

        public void CommitPurchase(string lootId, int amount)
        {
            if (_receiver == null) return;
            var req = new LootTransferRequest(_merchantId, _receiver.Id, new LootId(lootId), amount, 0);
            _receiver.CommitReceive(req);
        }

        public bool ValidateSale(string lootId, int amount)
        {
            if (_receiver == null) return false;
            var req = new LootTransferRequest(_receiver.Id, _merchantId, new LootId(lootId), amount, 0);
            return _receiver.ValidateExtraction(req) == LootTransferFailureReason.None;
        }

        public void CommitSale(string lootId, int amount)
        {
            if (_receiver == null) return;
            var req = new LootTransferRequest(_receiver.Id, _merchantId, new LootId(lootId), amount, 0);
            _receiver.CommitExtraction(req);
        }
    }
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
        _shopService = shopService;
        _localProfileId = profileId;
        _localOrchestrator = new MerchantTransactionOrchestrator(_shopService, _catalog, this, _localProfileId);
        _localOrchestrator.TransactionCompleted += OnLocalTransactionCompleted;
        _localOrchestrator.LocalPurchaseSucceeded += RecordLocalPurchase;
    }

    private void OnLocalTransactionCompleted(MerchantTransactionResult result)
    {
        // If a purchase was successful locally, increment our local counter so the UI knows
        // to disable the button if we hit the limit.
        if (result == MerchantTransactionResult.Success && _localOrchestrator != null)
        {
            // Removed obsolete comment
        }
        
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
        if (_localOrchestrator != null)
        {
            _localOrchestrator.TransactionCompleted -= OnLocalTransactionCompleted;
            _localOrchestrator.LocalPurchaseSucceeded -= RecordLocalPurchase;
            _localOrchestrator = null;
        }
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
        
        PlayerLootReceiver receiver = null;
        if (Runner.TryGetPlayerObject(source, out var networkObject) && networkObject != null)
        {
            receiver = networkObject.GetComponent<PlayerLootReceiver>();
        }

        if (receiver == null)
        {
            var receivers = FindObjectsByType<PlayerLootReceiver>(FindObjectsSortMode.None);
            foreach (var r in receivers)
            {
                if (r.Object != null && r.Object.InputAuthority == source)
                {
                    receiver = r;
                    break;
                }
            }
        }

        if (receiver != null && receiver.HasStateAuthority)
        {
            if (_requestValidator.TryProcessPurchaseRequest(source, new PlayerLootReceiverHandler(receiver, new EntityId((int)Object.Id.Raw)), clientSequence, lootId, amount, _catalog, out bool isApproved, out ShopTransactionId txId))
            {
                Rpc_PurchaseResponse(source, clientSequence, isApproved, txId.Timestamp, txId.Value);
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void Rpc_RequestSale(string lootId, int amount, int clientSequence, RpcInfo info = default)
    {
        PlayerRef source = info.Source.IsNone ? Runner.LocalPlayer : info.Source;
        Debug.Log($"[ShopTransaction] TownMerchantNetworkController.Rpc_RequestSale (Server): LootId={lootId}, Amount={amount}, Seq={clientSequence}, Source={source}");
        
        if (_requestValidator == null) return;
        
        PlayerLootReceiver receiver = null;
        if (Runner.TryGetPlayerObject(source, out var networkObject) && networkObject != null)
        {
            receiver = networkObject.GetComponent<PlayerLootReceiver>();
        }
        else
        {
            Debug.Log($"[ShopTransaction] Server: Runner.TryGetPlayerObject failed or returned null for Source={source}");
        }

        if (receiver == null)
        {
            var receivers = FindObjectsByType<PlayerLootReceiver>(FindObjectsSortMode.None);
            foreach (var r in receivers)
            {
                if (r.Object != null && r.Object.InputAuthority == source)
                {
                    Debug.Log($"[ShopTransaction] Server: Found receiver via FindObjectsByType fallback.");
                    receiver = r;
                    break;
                }
            }
        }
        
        if (receiver == null)
        {
            Debug.LogError($"[ShopTransaction] Server: Receiver is still null! Request will fail.");
        }

        if (receiver != null && receiver.HasStateAuthority)
        {
            if (_requestValidator.TryProcessSaleRequest(source, new PlayerLootReceiverHandler(receiver, new EntityId((int)Object.Id.Raw)), clientSequence, lootId, amount, _catalog, out bool isApproved, out ShopTransactionId txId))
            {
                Debug.Log($"[ShopTransaction] Server: TryProcessSaleRequest processed. isApproved={isApproved}, txId={txId.Timestamp}");
                Rpc_SaleResponse(source, clientSequence, isApproved, txId.Timestamp, txId.Value);
            }
            else
            {
                Debug.Log($"[ShopTransaction] Server: TryProcessSaleRequest returned false.");
            }
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
}
