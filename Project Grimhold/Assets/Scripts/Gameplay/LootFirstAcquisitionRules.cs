/// <summary>
/// Pure provenance arithmetic shared by source validation, query and the single extraction commit.
/// </summary>
public static class LootFirstAcquisitionRules
{
    public static bool TryResolveExtraction(
        int totalAmount,
        int eligibleAmount,
        int requestedAmount,
        out int consumedEligibleAmount,
        out int remainingTotalAmount,
        out int remainingEligibleAmount)
    {
        consumedEligibleAmount = 0;
        remainingTotalAmount = totalAmount;
        remainingEligibleAmount = eligibleAmount;
        if (totalAmount <= 0 || eligibleAmount < 0 || eligibleAmount > totalAmount ||
            requestedAmount <= 0 || requestedAmount > totalAmount)
        {
            return false;
        }

        consumedEligibleAmount = System.Math.Min(eligibleAmount, requestedAmount);
        remainingTotalAmount = totalAmount - requestedAmount;
        remainingEligibleAmount = eligibleAmount - consumedEligibleAmount;
        return remainingEligibleAmount >= 0 && remainingEligibleAmount <= remainingTotalAmount;
    }
}
