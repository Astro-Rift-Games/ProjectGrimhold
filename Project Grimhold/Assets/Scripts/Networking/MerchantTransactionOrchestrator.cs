using System;
using System.Collections.Generic;
using UnityEngine;

public interface IMasterClientRpcSender
{
    void SendPurchaseRequest(LootId lootId, int amount, int clientSequence);
    void SendSaleRequest(LootId lootId, int amount, int clientSequence);
}

/// <summary>
/// Core orchestrator for the local client. Manages in-flight transactions, 
/// deduplicates local requests, and bridges the network approval with the local atomic store.
/// </summary>
public sealed class MerchantTransactionOrchestrator
{
    private readonly IShopTransactionService _shopService;
    private readonly LootDefinitionCatalog _catalog;
    private readonly IMasterClientRpcSender _rpcSender;
    private readonly ProfileId _profileId;

    private readonly Dictionary<int, PendingTransaction> _pendingTransactions = new();
    private int _nextSequence = 1;

    public event Action<MerchantTransactionResult> TransactionCompleted;
    public event Action<string, int> LocalPurchaseSucceeded;

    private readonly struct PendingTransaction
    {
        public readonly LootId LootId;
        public readonly int Amount;
        public readonly MerchantOperationType OperationType;

        public PendingTransaction(LootId lootId, int amount, MerchantOperationType operationType)
        {
            LootId = lootId;
            Amount = amount;
            OperationType = operationType;
        }
    }

    public MerchantTransactionOrchestrator(
        IShopTransactionService shopService, 
        LootDefinitionCatalog catalog, 
        IMasterClientRpcSender rpcSender,
        ProfileId profileId)
    {
        _shopService = shopService ?? throw new ArgumentNullException(nameof(shopService));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _rpcSender = rpcSender ?? throw new ArgumentNullException(nameof(rpcSender));
        _profileId = profileId;
    }

    public void RequestPurchase(LootId lootId, int amount)
    {
        if (amount <= 0 || !_catalog.TryGet(lootId.Value, out _))
        {
            TransactionCompleted?.Invoke(MerchantTransactionResult.InvalidRequest);
            return;
        }

        int seq = _nextSequence++;
        _pendingTransactions[seq] = new PendingTransaction(lootId, amount, MerchantOperationType.Purchase);
        _rpcSender.SendPurchaseRequest(lootId, amount, seq);
    }

    public void RequestSale(LootId lootId, int amount)
    {
        Debug.Log($"[ShopTransaction] MerchantTransactionOrchestrator.RequestSale: LootId={lootId.Value}, Amount={amount}");
        if (amount <= 0 || !_catalog.TryGet(lootId.Value, out _))
        {
            TransactionCompleted?.Invoke(MerchantTransactionResult.InvalidRequest);
            return;
        }

        int seq = _nextSequence++;
        _pendingTransactions[seq] = new PendingTransaction(lootId, amount, MerchantOperationType.Sale);
        _rpcSender.SendSaleRequest(lootId, amount, seq);
    }

    public void OnPurchaseResponseReceived(int clientSequence, bool isApproved, ShopTransactionId transactionId)
    {
        if (!_pendingTransactions.Remove(clientSequence, out var pending) || pending.OperationType != MerchantOperationType.Purchase)
        {
            // The request is no longer pending (e.g. duplicate response received due to network retry), ignore it.
            return;
        }

        if (!isApproved)
        {
            TransactionCompleted?.Invoke(MerchantTransactionResult.RejectedByMerchant);
            return;
        }

        if (_catalog.TryGet(pending.LootId.Value, out var definition))
        {
            long price = definition.ExtractionValuePerUnit * pending.Amount;
            var result = _shopService.TryExecutePurchase(_profileId, pending.LootId, pending.Amount, price, transactionId);
            
            if (result == StashOperationResult.Success)
            {
                LocalPurchaseSucceeded?.Invoke(pending.LootId.Value, pending.Amount);
            }

            TransactionCompleted?.Invoke(MapResult(result, MerchantOperationType.Purchase));
        }
        else
        {
            TransactionCompleted?.Invoke(MerchantTransactionResult.InvalidRequest);
        }
    }

    public void OnSaleResponseReceived(int clientSequence, bool isApproved, ShopTransactionId transactionId)
    {
        Debug.Log($"[ShopTransaction] MerchantTransactionOrchestrator.OnSaleResponseReceived: Seq={clientSequence}, isApproved={isApproved}");
        if (!_pendingTransactions.Remove(clientSequence, out var pending) || pending.OperationType != MerchantOperationType.Sale)
        {
            // Duplicate or unknown response
            return;
        }

        if (!isApproved)
        {
            TransactionCompleted?.Invoke(MerchantTransactionResult.RejectedByMerchant);
            return;
        }

        if (_catalog.TryGet(pending.LootId.Value, out var definition))
        {
            long sellValue = definition.SellValuePerUnit * pending.Amount;
            Debug.Log($"[ShopTransaction] Local execution: SellValue={sellValue}");
            var result = _shopService.TryExecuteSale(_profileId, pending.LootId, pending.Amount, sellValue, transactionId);
            Debug.Log($"[ShopTransaction] Local execution result: {result}");
            
            TransactionCompleted?.Invoke(MapResult(result, MerchantOperationType.Sale));
        }
        else
        {
            TransactionCompleted?.Invoke(MerchantTransactionResult.InvalidRequest);
        }
    }

    private MerchantTransactionResult MapResult(StashOperationResult stashResult, MerchantOperationType type)
    {
        return stashResult switch
        {
            StashOperationResult.Success => MerchantTransactionResult.Success,
            StashOperationResult.AlreadyApplied => MerchantTransactionResult.AlreadyApplied,
            StashOperationResult.InvalidInventory => type == MerchantOperationType.Purchase ? MerchantTransactionResult.InsufficientFunds : MerchantTransactionResult.MissingItems, 
            StashOperationResult.PersistenceFailed => MerchantTransactionResult.Timeout,
            _ => MerchantTransactionResult.InvalidRequest
        };
    }
}
