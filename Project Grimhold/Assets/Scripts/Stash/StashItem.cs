using System;

/// <summary>
/// Extensible model representing an item in a player's stash.
/// Prepared for future expansion with instance-specific state (durability, affixes, etc.).
/// </summary>
public readonly struct StashItem : IEquatable<StashItem>
{
    public LootId LootId { get; }
    public int Amount { get; }
    /// <summary>Physical Town location; -1 denotes an aggregate input awaiting placement.</summary>
    public int Slot { get; }

    public bool IsValid => LootId.IsValid && Amount > 0;

    public StashItem(LootId lootId, int amount) : this(lootId, amount, -1) { }

    public StashItem(LootId lootId, int amount, int slot)
    {
        LootId = lootId;
        Amount = amount;
        Slot = slot;
    }

    public StashItem AtSlot(int slot) => new(LootId, Amount, slot);

    public bool Equals(StashItem other)
    {
        return LootId.Equals(other.LootId) && Amount == other.Amount && Slot == other.Slot;
    }

    public override bool Equals(object obj)
    {
        return obj is StashItem other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (((LootId.GetHashCode() * 397) ^ Amount) * 397) ^ Slot;
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
