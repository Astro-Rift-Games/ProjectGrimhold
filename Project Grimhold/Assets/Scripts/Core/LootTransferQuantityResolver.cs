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
}
