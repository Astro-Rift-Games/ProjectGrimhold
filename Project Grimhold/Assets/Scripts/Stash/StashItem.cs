using System;

/// <summary>
/// One physical Town entry. Equipment carries one immutable instance; stackable loot has none.
/// </summary>
public readonly struct StashItem : IEquatable<StashItem>
{
    public LootId LootId { get; }
    public int Amount { get; }
    /// <summary>Physical Town location; -1 denotes an aggregate input awaiting placement.</summary>
    public int Slot { get; }

    public EquipmentInstance Instance { get; }
    public string ExpeditionId { get; }
    public string Origin { get; }
    public bool IsValid => LootId.IsValid && Amount > 0 && (Instance == null || Amount == 1);

    public StashItem(LootId lootId, int amount) : this(lootId, amount, -1, null, string.Empty, string.Empty) { }

    public StashItem(LootId lootId, int amount, int slot, EquipmentInstance instance = null, string expeditionId = null, string origin = null)
    {
        LootId = lootId;
        Amount = amount;
        Slot = slot;
        Instance = instance;
        ExpeditionId = expeditionId ?? string.Empty;
        Origin = origin ?? string.Empty;
    }

    public StashItem AtSlot(int slot) => new(LootId, Amount, slot, Instance, ExpeditionId, Origin);
    public StashItem WithOrigin(string expeditionId, string origin) => new(LootId, Amount, Slot, Instance, expeditionId ?? string.Empty, origin ?? string.Empty);

    public bool Equals(StashItem other)
    {
        return LootId.Equals(other.LootId) &&
               Amount == other.Amount &&
               Slot == other.Slot &&
               Equals(Instance, other.Instance) &&
               string.Equals(ExpeditionId, other.ExpeditionId, StringComparison.Ordinal) &&
               string.Equals(Origin, other.Origin, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is StashItem other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = ((((LootId.GetHashCode() * 397) ^ Amount) * 397) ^ Slot) * 397 ^ (Instance?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ (ExpeditionId != null ? StringComparer.Ordinal.GetHashCode(ExpeditionId) : 0);
            hash = (hash * 397) ^ (Origin != null ? StringComparer.Ordinal.GetHashCode(Origin) : 0);
            return hash;
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
