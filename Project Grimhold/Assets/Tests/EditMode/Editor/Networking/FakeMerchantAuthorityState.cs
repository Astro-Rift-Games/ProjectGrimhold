/// <summary>
/// Plain-array stand-in for the merchant's replicated Fusion state.
/// </summary>
public sealed class FakeMerchantAuthorityState : IMerchantAuthorityState
{
    private readonly int[] _quantities;
    private readonly MerchantTradeRecord[] _records;
    private readonly MerchantTradeStockLine[] _stockLines;

    public FakeMerchantAuthorityState(
        int stockSlotCapacity = TownMerchantNetworkController.MaxStockSlots,
        int tradeRecordCapacity = TownMerchantNetworkController.MaxTradeRecords,
        int tradeStockLineCapacity = TownMerchantNetworkController.MaxTradeStockLines)
    {
        _quantities = new int[stockSlotCapacity];
        _records = new MerchantTradeRecord[tradeRecordCapacity];
        _stockLines = new MerchantTradeStockLine[tradeStockLineCapacity];
    }

    public bool CanMutate { get; set; } = true;
    public int StockChangeCount { get; private set; }

    public int StockSlotCapacity => _quantities.Length;
    public int GetStockQuantity(int slot) => _quantities[slot];
    public void SetStockQuantity(int slot, int quantity) => _quantities[slot] = quantity;

    public int TradeRecordCapacity => _records.Length;
    public MerchantTradeRecord GetTradeRecord(int index) => _records[index];
    public void SetTradeRecord(int index, MerchantTradeRecord record) => _records[index] = record;

    public int TradeStockLineCapacity => _stockLines.Length;
    public MerchantTradeStockLine GetTradeStockLine(int index) => _stockLines[index];
    public void SetTradeStockLine(int index, MerchantTradeStockLine line) => _stockLines[index] = line;

    public void MarkStockChanged() => StockChangeCount++;

    public int CountRecords(MerchantTradeRecordStatus status)
    {
        int count = 0;
        foreach (MerchantTradeRecord record in _records)
        {
            if (record.RecordStatus == status)
            {
                count++;
            }
        }

        return count;
    }

    public int ActiveStockLineCount
    {
        get
        {
            int count = 0;
            foreach (MerchantTradeStockLine line in _stockLines)
            {
                if (line.IsActive)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
