/// <summary>
/// One reason a merchant trade preview blocks confirmation. <see cref="LootId"/> names the
/// affected line's loot, or is default when the reason applies to the whole trade.
/// </summary>
public readonly struct MerchantTradeBlock
{
    public MerchantTradeBlockReason Reason { get; }
    public LootId LootId { get; }

    public MerchantTradeBlock(MerchantTradeBlockReason reason, LootId lootId = default)
    {
        Reason = reason;
        LootId = lootId;
    }
}
