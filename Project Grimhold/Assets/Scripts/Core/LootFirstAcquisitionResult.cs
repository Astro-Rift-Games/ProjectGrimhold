using System;

/// <summary>
/// Immutable authoritative provenance resolved for a validated loot extraction.
/// It remains separate from <see cref="LootTransferRequest"/>.
/// </summary>
public readonly struct LootFirstAcquisitionResult
{
    public int EligibleAmount { get; }

    public LootFirstAcquisitionResult(int eligibleAmount)
    {
        if (eligibleAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eligibleAmount));
        }

        EligibleAmount = eligibleAmount;
    }

    public bool IsValidFor(in LootTransferRequest request) =>
        request.IsValid && EligibleAmount >= 0 && EligibleAmount <= request.RequestedAmount;

    public static LootFirstAcquisitionResult None => default;
}
