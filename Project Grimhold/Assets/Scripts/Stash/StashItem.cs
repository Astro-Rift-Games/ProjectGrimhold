using System;

/// <summary>
/// Extensible model representing an item in a player's stash.
/// Prepared for future expansion with instance-specific state (durability, affixes, etc.).
/// </summary>
public readonly struct StashItem : IEquatable<StashItem>
{
    public LootId LootId { get; }
    public int Amount { get; }

    public bool IsValid => LootId.IsValid && Amount > 0;

    public StashItem(LootId lootId, int amount)
    {
        LootId = lootId;
        Amount = amount;
    }

    public bool Equals(StashItem other)
    {
        return LootId.Equals(other.LootId) && Amount == other.Amount;
    }

    public override bool Equals(object obj)
    {
        return obj is StashItem other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (LootId.GetHashCode() * 397) ^ Amount;
        }
    }

    public static bool operator ==(StashItem left, StashItem right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(StashItem left, StashItem right)
    {
        return !left.Equals(right);
    }
}
