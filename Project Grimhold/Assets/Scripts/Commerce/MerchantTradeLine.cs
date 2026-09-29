/// <summary>
/// One immutable buy or sell line of a pending merchant trade.
/// </summary>
public readonly struct MerchantTradeLine
{
    public LootId LootId { get; }
    public int Amount { get; }

    public MerchantTradeLine(LootId lootId, int amount)
    {
        LootId = lootId;
        Amount = amount;
    }
}
