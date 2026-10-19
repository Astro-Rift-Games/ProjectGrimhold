using System;

/// <summary>One LootId and quantity shown on the Results screen.</summary>
public readonly struct RaidResultsLootEntry : IEquatable<RaidResultsLootEntry>
{
    public RaidResultsLootEntry(LootId lootId, int amount)
    {
        LootId = lootId;
        Amount = amount;
    }

    public LootId LootId { get; }
    public int Amount { get; }
    public bool IsValid => LootId.IsValid && Amount > 0;

    public bool Equals(RaidResultsLootEntry other) =>
        LootId == other.LootId && Amount == other.Amount;
    public override bool Equals(object obj) => obj is RaidResultsLootEntry other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(LootId, Amount);
}
