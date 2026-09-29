/// <summary>
/// One trade line priced by State Authority: loot, units and the authority-resolved unit price.
/// </summary>
public readonly struct MerchantPricedTradeLine
{
    public LootId LootId { get; }
    public int Amount { get; }
    public int UnitPrice { get; }

    public MerchantPricedTradeLine(LootId lootId, int amount, int unitPrice)
    {
        LootId = lootId;
        Amount = amount;
        UnitPrice = unitPrice;
    }

    /// <summary>Line total; cannot overflow because both factors are 32-bit.</summary>
    public long Total => (long)UnitPrice * Amount;
}
