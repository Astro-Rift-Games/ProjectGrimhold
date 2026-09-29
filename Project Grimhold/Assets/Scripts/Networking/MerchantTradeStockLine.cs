using Fusion;

/// <summary>
/// Replicated stock effect of one pending trade on one finite stock slot. A purchase line
/// reserves its units; a sale line adds its units to stock only when the trade is persisted.
/// </summary>
public struct MerchantTradeStockLine : INetworkStruct
{
    public NetworkBool IsActive;
    public NetworkBool IsPurchase;
    public int RecordIndex;
    public int StockSlot;
    public int Amount;

    public bool Reserves(int stockSlot) => IsActive && IsPurchase && StockSlot == stockSlot;

    public static MerchantTradeStockLine Create(int recordIndex, int stockSlot, int amount, bool isPurchase)
    {
        return new MerchantTradeStockLine
        {
            IsActive = true,
            IsPurchase = isPurchase,
            RecordIndex = recordIndex,
            StockSlot = stockSlot,
            Amount = amount
        };
    }
}
