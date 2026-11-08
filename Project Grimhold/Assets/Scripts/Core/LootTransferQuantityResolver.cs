/// <summary>
/// Resolves a client quantity intention against the source amount observed by State Authority.
/// </summary>
public static class LootTransferQuantityResolver
{
    /// <summary>
    /// Resolves one unit or the complete authoritative stack without accepting arbitrary client amounts.
    /// </summary>
    public static LootTransferFailureReason Resolve(
        LootTransferQuantityMode quantityMode,
        int availableAmount,
        out int requestedAmount)
    {
        requestedAmount = 0;
        if (quantityMode != LootTransferQuantityMode.SingleUnit &&
            quantityMode != LootTransferQuantityMode.FullStack)
        {
            return LootTransferFailureReason.InvalidAmount;
        }

        if (availableAmount <= 0)
        {
            return LootTransferFailureReason.InsufficientAmount;
        }

        requestedAmount = quantityMode == LootTransferQuantityMode.SingleUnit
            ? 1
            : availableAmount;
        return LootTransferFailureReason.None;
    }

    /// <summary>
    /// Also resolves an exact partial intention. The chosen amount is validated, never trusted:
    /// it must leave at least one unit in the authoritative source, and other modes must carry none.
    /// </summary>
    public static LootTransferFailureReason Resolve(
        LootTransferQuantityMode quantityMode,
        int availableAmount,
        int exactAmount,
        out int requestedAmount)
    {
        if (quantityMode != LootTransferQuantityMode.ExactAmount)
        {
            requestedAmount = 0;
            return exactAmount != 0
                ? LootTransferFailureReason.InvalidAmount
                : Resolve(quantityMode, availableAmount, out requestedAmount);
        }

        requestedAmount = 0;
        if (availableAmount <= 0 || exactAmount > availableAmount)
        {
            return LootTransferFailureReason.InsufficientAmount;
        }

        if (exactAmount < 1 || exactAmount >= availableAmount)
        {
            return LootTransferFailureReason.InvalidAmount;
        }

        requestedAmount = exactAmount;
        return LootTransferFailureReason.None;
    }
}
