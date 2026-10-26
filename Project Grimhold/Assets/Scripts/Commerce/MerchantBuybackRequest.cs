/// <summary>Buy-back intention. Loot and price are advisory; the backend resolves the entry.</summary>
public readonly struct MerchantBuybackRequest
{
    public ShopTransactionId TransactionId { get; }
    public string EntryId { get; }
    public LootId LootId { get; }
    public int Amount { get; }
    public long UnitPrice { get; }

    public MerchantBuybackRequest(ShopTransactionId transactionId, string entryId, LootId lootId, int amount, long unitPrice)
    {
        TransactionId = transactionId;
        EntryId = entryId;
        LootId = lootId;
        Amount = amount;
        UnitPrice = unitPrice;
    }
}
