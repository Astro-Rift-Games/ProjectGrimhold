/// <summary>
/// Storage of the merchant's shared stock and trade records. The merchant network controller
/// backs it with replicated Fusion state; tests back it with plain arrays. Only State Authority
/// may mutate it.
/// </summary>
public interface IMerchantAuthorityState
{
    bool CanMutate { get; }

    int StockSlotCapacity { get; }
    int GetStockQuantity(int slot);
    void SetStockQuantity(int slot, int quantity);

    int TradeRecordCapacity { get; }
    MerchantTradeRecord GetTradeRecord(int index);
    void SetTradeRecord(int index, MerchantTradeRecord record);

    int TradeStockLineCapacity { get; }
    MerchantTradeStockLine GetTradeStockLine(int index);
    void SetTradeStockLine(int index, MerchantTradeStockLine line);

    /// <summary>Signals observers that available stock changed.</summary>
    void MarkStockChanged();
}
