using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;


public enum MerchantOperationType { Purchase, Sale }

public interface IMerchantInventoryHandler
{
    bool ValidatePurchase(string lootId, int amount);
    void CommitPurchase(string lootId, int amount);
    bool ValidateSale(string lootId, int amount);
    void CommitSale(string lootId, int amount);
}

public readonly struct ProcessedRequestRecord
{
    public readonly string LootId;
    public readonly int Amount;
    public readonly MerchantOperationType OperationType;
    public readonly bool IsApproved;
    public readonly ShopTransactionId TransactionId;

    public ProcessedRequestRecord(string lootId, int amount, MerchantOperationType type, bool isApproved, ShopTransactionId transactionId)
    {
        LootId = lootId;
        Amount = amount;
        OperationType = type;
        IsApproved = isApproved;
        TransactionId = transactionId;
    }
}

/// <summary>
/// Validates shop transactions on the Master Client and maintains an in-memory session log 
/// of processed requests to ensure absolute idempotency against network retries.
/// </summary>
public sealed class MerchantRequestValidator
{
    private readonly Dictionary<(PlayerRef, int), ProcessedRequestRecord> _processedRequests = new();
    private readonly Dictionary<PlayerRef, Dictionary<string, int>> _playerPurchases = new();
    
    // For test stability, we allow overriding the time provider
    private readonly Func<long> _timestampProvider;
    private readonly Func<Guid> _guidProvider;
    private readonly IReadOnlyList<MerchantStockItem> _stock;

    public MerchantRequestValidator(IReadOnlyList<MerchantStockItem> stock = null, Func<long> timestampProvider = null, Func<Guid> guidProvider = null)
    {
        _stock = stock;
        _timestampProvider = timestampProvider ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _guidProvider = guidProvider ?? Guid.NewGuid;
    }

    /// <summary>
    /// Processes a purchase request. 
    /// Returns true if the request is valid or a valid duplicate retry.
    /// Returns false if there is a sequence conflict (same sequence, different payload).
    /// </summary>
    public bool TryProcessPurchaseRequest(
        PlayerRef player,
        IMerchantInventoryHandler inventoryHandler,
        int clientSequence, 
        string lootId, 
        int amount, 
        LootDefinitionCatalog catalog,
        out bool isApproved,
        out ShopTransactionId transactionId)
    {
        return TryProcessRequest(player, inventoryHandler, clientSequence, lootId, amount, MerchantOperationType.Purchase, catalog, out isApproved, out transactionId);
    }

    /// <summary>
    /// Processes a sale request.
    /// Returns true if the request is valid or a valid duplicate retry.
    /// Returns false if there is a sequence conflict (same sequence, different payload).
    /// </summary>
    public bool TryProcessSaleRequest(
        PlayerRef player,
        IMerchantInventoryHandler inventoryHandler,
        int clientSequence, 
        string lootId, 
        int amount, 
        LootDefinitionCatalog catalog,
        out bool isApproved,
        out ShopTransactionId transactionId)
    {
        return TryProcessRequest(player, inventoryHandler, clientSequence, lootId, amount, MerchantOperationType.Sale, catalog, out isApproved, out transactionId);
    }

    public void OnPlayerLeft(PlayerRef player)
    {
        // Cleanup memory for players that disconnected
        var keysToRemove = new List<(PlayerRef, int)>();
        foreach (var key in _processedRequests.Keys)
        {
            if (key.Item1 == player)
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _processedRequests.Remove(key);
        }
        
        _playerPurchases.Remove(player);
    }

    private bool TryProcessRequest(
        PlayerRef player,
        IMerchantInventoryHandler inventoryHandler,
        int clientSequence, 
        string lootId, 
        int amount, 
        MerchantOperationType type,
        LootDefinitionCatalog catalog,
        out bool isApproved,
        out ShopTransactionId transactionId)
    {
        var key = (player, clientSequence);

        if (_processedRequests.TryGetValue(key, out var record))
        {
            // If the payload exactly matches, it's a valid retry (e.g. lost response)
            if (string.Equals(record.LootId, lootId, StringComparison.Ordinal) && 
                record.Amount == amount && 
                record.OperationType == type)
            {
                isApproved = record.IsApproved;
                transactionId = record.TransactionId;
                return true;
            }
            
            // Conflict detected: the client reused a sequence number for a different request
            isApproved = false;
            transactionId = default;
            return false;
        }

        isApproved = amount > 0 && catalog != null && inventoryHandler != null;
        
        if (isApproved)
        {
            if (type == MerchantOperationType.Purchase)
            {
                if (!inventoryHandler.ValidatePurchase(lootId, amount) || !HasAvailableStock(player, lootId, amount))
                {
                    isApproved = false;
                    Debug.Log($"[ShopTransaction] Validator: Purchase rejected. ValidatePurchase/Stock failed.");
                }
            }
            else // Sale
            {
                bool isValidSale = inventoryHandler.ValidateSale(lootId, amount);
                Debug.Log($"[ShopTransaction] Validator: ValidateSale returned {isValidSale}");
                if (!isValidSale)
                {
                    isApproved = false;
                }
            }
        }
        else
        {
            Debug.Log($"[ShopTransaction] Validator: Initial validation failed (Amount<=0 or null catalog/handler).");
        }

        if (isApproved)
        {
            transactionId = new ShopTransactionId(_timestampProvider(), _guidProvider());
            
            if (type == MerchantOperationType.Purchase)
            {
                inventoryHandler.CommitPurchase(lootId, amount);
                RecordPurchase(player, lootId, amount);
            }
            else
            {
                inventoryHandler.CommitSale(lootId, amount);
            }
        }
        else
        {
            transactionId = default;
        }

        var newRecord = new ProcessedRequestRecord(lootId, amount, type, isApproved, transactionId);
        _processedRequests[key] = newRecord;

        return true;
    }

    private bool HasAvailableStock(PlayerRef player, string lootId, int amount)
    {
        if (_stock == null) return true; // No stock config means unlimited (or we can assume 0. Let's say unlimited for backwards compat).
        
        foreach (var item in _stock)
        {
            if (item.Item != null && item.Item.Id == lootId)
            {
                if (item.MaxQuantity == -1) return true;
                
                int alreadyPurchased = 0;
                if (_playerPurchases.TryGetValue(player, out var purchases))
                {
                    purchases.TryGetValue(lootId, out alreadyPurchased);
                }
                
                return (alreadyPurchased + amount) <= item.MaxQuantity;
            }
        }
        
        return false; // Item not found in stock list for this merchant
    }

    private void RecordPurchase(PlayerRef player, string lootId, int amount)
    {
        if (!_playerPurchases.TryGetValue(player, out var purchases))
        {
            purchases = new Dictionary<string, int>();
            _playerPurchases[player] = purchases;
        }
        
        if (!purchases.ContainsKey(lootId))
        {
            purchases[lootId] = 0;
        }
        
        purchases[lootId] += amount;
    }
}
