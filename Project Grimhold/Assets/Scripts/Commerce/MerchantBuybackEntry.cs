/// <summary>A backend-owned sale line from the current account's Town buy-back epoch.</summary>
public readonly struct MerchantBuybackEntry
{
    public string EntryId { get; }
    public LootId LootId { get; }
    public int Amount { get; }
    public long UnitPrice { get; }

    public MerchantBuybackEntry(string entryId, LootId lootId, int amount, long unitPrice)
    {
        EntryId = entryId;
        LootId = lootId;
        Amount = amount;
        UnitPrice = unitPrice;
    }
}
