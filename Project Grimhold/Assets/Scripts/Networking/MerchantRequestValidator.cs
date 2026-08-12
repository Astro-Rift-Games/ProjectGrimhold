using Fusion;
using System;
using System.Collections.Generic;

public enum MerchantOperationType { Purchase, Sale }

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
    
    // For test stability, we allow overriding the time provider
    private readonly Func<long> _timestampProvider;
    private readonly Func<Guid> _guidProvider;

    public MerchantRequestValidator(Func<long> timestampProvider = null, Func<Guid> guidProvider = null)
    {
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
        int clientSequence, 
        string lootId, 
        int amount, 
        LootDefinitionCatalog catalog,
        out bool isApproved,
        out ShopTransactionId transactionId)
    {
        return TryProcessRequest(player, clientSequence, lootId, amount, MerchantOperationType.Purchase, catalog, out isApproved, out transactionId);
    }

    /// <summary>
    /// Processes a sale request.
    /// Returns true if the request is valid or a valid duplicate retry.
    /// Returns false if there is a sequence conflict (same sequence, different payload).
    /// </summary>
    public bool TryProcessSaleRequest(
        PlayerRef player, 
        int clientSequence, 
        string lootId, 
        int amount, 
        LootDefinitionCatalog catalog,
        out bool isApproved,
        out ShopTransactionId transactionId)
    {
        return TryProcessRequest(player, clientSequence, lootId, amount, MerchantOperationType.Sale, catalog, out isApproved, out transactionId);
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
    }

    private bool TryProcessRequest(
        PlayerRef player, 
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

        isApproved = amount > 0 && catalog != null && catalog.TryGet(lootId, out _);
        
        if (isApproved)
        {
            transactionId = new ShopTransactionId(_timestampProvider(), _guidProvider());
        }
        else
        {
            transactionId = default;
        }

        var newRecord = new ProcessedRequestRecord(lootId, amount, type, isApproved, transactionId);
        _processedRequests[key] = newRecord;

        return true;
    }
}
