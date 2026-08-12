using Fusion;
using UnityEngine;
using System;

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

    private MerchantRequestValidator _requestValidator;
    private MerchantTransactionOrchestrator _localOrchestrator;
    private IShopTransactionService _shopService;
    private ProfileId _localProfileId;

    public event Action<MerchantTransactionResult> LocalTransactionCompleted;

    public void InitializeLocalClient(IShopTransactionService shopService, ProfileId profileId)
    {
        _shopService = shopService;
        _localProfileId = profileId;
        _localOrchestrator = new MerchantTransactionOrchestrator(_shopService, _catalog, this, _localProfileId);
        _localOrchestrator.TransactionCompleted += OnLocalTransactionCompleted;
    }

    private void OnLocalTransactionCompleted(MerchantTransactionResult result)
    {
        LocalTransactionCompleted?.Invoke(result);
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            _requestValidator = new MerchantRequestValidator();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_localOrchestrator != null)
        {
            _localOrchestrator.TransactionCompleted -= OnLocalTransactionCompleted;
            _localOrchestrator = null;
        }
    }

    public void RequestPurchase(LootId lootId, int amount)
    {
        _localOrchestrator?.RequestPurchase(lootId, amount);
    }

    public void RequestSale(LootId lootId, int amount)
    {
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
    [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    private void Rpc_RequestPurchase(string lootId, int amount, int clientSequence, RpcInfo info = default)
    {
        if (_requestValidator == null) return;

        if (_requestValidator.TryProcessPurchaseRequest(info.Source, clientSequence, lootId, amount, _catalog, out bool isApproved, out ShopTransactionId txId))
        {
            Rpc_PurchaseResponse(info.Source, clientSequence, isApproved, txId.Timestamp, txId.Value);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    private void Rpc_RequestSale(string lootId, int amount, int clientSequence, RpcInfo info = default)
    {
        if (_requestValidator == null) return;

        if (_requestValidator.TryProcessSaleRequest(info.Source, clientSequence, lootId, amount, _catalog, out bool isApproved, out ShopTransactionId txId))
        {
            Rpc_SaleResponse(info.Source, clientSequence, isApproved, txId.Timestamp, txId.Value);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void Rpc_PurchaseResponse([RpcTarget] PlayerRef target, int clientSequence, bool isApproved, long timestamp, Guid txGuid)
    {
        var txId = isApproved ? new ShopTransactionId(timestamp, txGuid) : default;
        _localOrchestrator?.OnPurchaseResponseReceived(clientSequence, isApproved, txId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void Rpc_SaleResponse([RpcTarget] PlayerRef target, int clientSequence, bool isApproved, long timestamp, Guid txGuid)
    {
        var txId = isApproved ? new ShopTransactionId(timestamp, txGuid) : default;
        _localOrchestrator?.OnSaleResponseReceived(clientSequence, isApproved, txId);
    }
}
