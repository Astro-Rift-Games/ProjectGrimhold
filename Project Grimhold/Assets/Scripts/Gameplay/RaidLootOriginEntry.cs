using System;

/// <summary>One LootId plus one positive origin quantity in a complete ownership snapshot.</summary>
public readonly struct RaidLootOriginEntry : IEquatable<RaidLootOriginEntry>
{
    public RaidLootOriginEntry(LootId lootId, RaidLootOrigin origin, int amount)
    {
        LootId = lootId;
        Origin = origin;
        Amount = amount;
    }

    public LootId LootId { get; }
    public RaidLootOrigin Origin { get; }
    public int Amount { get; }
    public bool IsValid => LootId.IsValid && Origin.IsValid && Amount > 0;

    public bool Equals(RaidLootOriginEntry other) =>
        LootId == other.LootId && Origin == other.Origin && Amount == other.Amount;
    public override bool Equals(object obj) => obj is RaidLootOriginEntry other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(LootId, Origin, Amount);
}
