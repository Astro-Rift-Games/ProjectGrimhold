using System;

/// <summary>One positive quantity associated with one immutable Raid loot origin.</summary>
public readonly struct RaidLootOriginBucket : IEquatable<RaidLootOriginBucket>
{
    public RaidLootOriginBucket(RaidLootOrigin origin, int amount)
    {
        Origin = origin;
        Amount = amount;
    }

    public RaidLootOrigin Origin { get; }
    public int Amount { get; }
    public bool IsValid => Origin.IsValid && Amount > 0;

    public bool Equals(RaidLootOriginBucket other) => Origin == other.Origin && Amount == other.Amount;
    public override bool Equals(object obj) => obj is RaidLootOriginBucket other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Origin, Amount);
}
