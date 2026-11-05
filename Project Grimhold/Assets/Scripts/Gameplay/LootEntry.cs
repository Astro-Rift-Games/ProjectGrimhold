using System;

/// <summary>
/// Detached loot snapshot: an aggregate stack or one ID-free equipment unit.
/// </summary>
public readonly struct LootEntry : IEquatable<LootEntry>
{
    public LootId LootId { get; }
    public int Amount { get; }
    public WeaponInstanceModifiers? Modifiers { get; }
    public bool IsEquipmentUnit { get; }

    public bool IsValid => LootId.IsValid && Amount > 0 && (!IsEquipmentUnit || Amount == 1);

    public LootEntry(LootId lootId, int amount, WeaponInstanceModifiers? modifiers = null, bool isEquipmentUnit = false)
    {
        LootId = lootId;
        Amount = amount;
        Modifiers = modifiers?.Normalize();
        IsEquipmentUnit = isEquipmentUnit || modifiers.HasValue;
    }

    public bool Equals(LootEntry other) => LootId.Equals(other.LootId) && Amount == other.Amount &&
        IsEquipmentUnit == other.IsEquipmentUnit && Nullable.Equals(Modifiers, other.Modifiers);

    public override bool Equals(object obj) => obj is LootEntry other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (((LootId.GetHashCode() * 397) ^ Amount) * 397 ^ IsEquipmentUnit.GetHashCode()) * 397 ^
                (Modifiers?.GetHashCode() ?? 0);
        }
    }

    public static bool operator ==(LootEntry left, LootEntry right) => left.Equals(right);

    public static bool operator !=(LootEntry left, LootEntry right) => !left.Equals(right);
}
