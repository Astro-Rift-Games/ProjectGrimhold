using System;

/// <summary>Eligibility quantities for one extracted LootId.</summary>
public readonly struct RaidLootEligibilityEntry : IEquatable<RaidLootEligibilityEntry>
{
    public RaidLootEligibilityEntry(LootId lootId, int totalAmount, int eligibleAmount)
    {
        LootId = lootId;
        TotalAmount = totalAmount;
        EligibleAmount = eligibleAmount;
    }

    public LootId LootId { get; }
    public int TotalAmount { get; }
    public int EligibleAmount { get; }
    public int IneligibleAmount => TotalAmount - EligibleAmount;
    public bool IsValid => LootId.IsValid && TotalAmount > 0 &&
                           EligibleAmount >= 0 && EligibleAmount <= TotalAmount;

    public bool Equals(RaidLootEligibilityEntry other) =>
        LootId == other.LootId && TotalAmount == other.TotalAmount &&
        EligibleAmount == other.EligibleAmount;
    public override bool Equals(object obj) => obj is RaidLootEligibilityEntry other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(LootId, TotalAmount, EligibleAmount);
}
