using System;

/// <summary>One equipped weapon with its authored and attribute-scaled damage.</summary>
public readonly struct TownWeaponStatistics : IEquatable<TownWeaponStatistics>
{
    public EquipmentSlot Slot { get; }
    public LootId LootId { get; }
    public string DisplayName { get; }
    public DamageType DamageType { get; }
    public float BaseDamage { get; }
    public float EffectiveDamage { get; }

    internal TownWeaponStatistics(
        EquipmentSlot slot,
        LootId lootId,
        string displayName,
        DamageType damageType,
        float baseDamage,
        float effectiveDamage)
    {
        Slot = slot;
        LootId = lootId;
        DisplayName = displayName ?? string.Empty;
        DamageType = damageType;
        BaseDamage = baseDamage;
        EffectiveDamage = effectiveDamage;
    }

    public bool Equals(TownWeaponStatistics other) =>
        Slot == other.Slot && LootId.Equals(other.LootId) &&
        string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal) &&
        DamageType == other.DamageType && BaseDamage.Equals(other.BaseDamage) &&
        EffectiveDamage.Equals(other.EffectiveDamage);

    public override bool Equals(object obj) => obj is TownWeaponStatistics other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Slot, LootId, DisplayName, DamageType, BaseDamage, EffectiveDamage);
}
