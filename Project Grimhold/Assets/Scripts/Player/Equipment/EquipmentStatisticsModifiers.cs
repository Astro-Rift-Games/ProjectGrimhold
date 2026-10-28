using System;

/// <summary>Immutable aggregate contributed by the player's currently equipped armor.</summary>
public readonly struct EquipmentStatisticsModifiers : IEquatable<EquipmentStatisticsModifiers>
{
    public int PhysicalDefense { get; }
    public int MagicalDefense { get; }
    public int MaximumHealthModifier { get; }
    public int MaximumStaminaModifier { get; }
    public int MaximumManaModifier { get; }

    /// <summary>Equipment knockback resistance (RE), with no primary-attribute scaling.</summary>
    public int KnockbackResistance { get; }

    internal EquipmentStatisticsModifiers(
        int physicalDefense,
        int magicalDefense,
        int maximumHealthModifier,
        int maximumStaminaModifier,
        int maximumManaModifier,
        int knockbackResistance = 0)
    {
        PhysicalDefense = physicalDefense;
        MagicalDefense = magicalDefense;
        MaximumHealthModifier = maximumHealthModifier;
        MaximumStaminaModifier = maximumStaminaModifier;
        MaximumManaModifier = maximumManaModifier;
        KnockbackResistance = knockbackResistance;
    }

    public static bool TryCreate(
        int physicalDefense,
        int magicalDefense,
        int maximumHealthModifier,
        int maximumStaminaModifier,
        int maximumManaModifier,
        out EquipmentStatisticsModifiers modifiers,
        int knockbackResistance = 0)
    {
        modifiers = default;
        if (physicalDefense < 0 || magicalDefense < 0 ||
            maximumHealthModifier < 0 || maximumStaminaModifier < 0 ||
            maximumManaModifier < 0 || knockbackResistance < 0)
        {
            return false;
        }

        modifiers = new EquipmentStatisticsModifiers(
            physicalDefense,
            magicalDefense,
            maximumHealthModifier,
            maximumStaminaModifier,
            maximumManaModifier,
            knockbackResistance);
        return true;
    }

    public bool Equals(EquipmentStatisticsModifiers other) =>
        PhysicalDefense == other.PhysicalDefense &&
        MagicalDefense == other.MagicalDefense &&
        MaximumHealthModifier == other.MaximumHealthModifier &&
        MaximumStaminaModifier == other.MaximumStaminaModifier &&
        MaximumManaModifier == other.MaximumManaModifier &&
        KnockbackResistance == other.KnockbackResistance;

    public override bool Equals(object obj) =>
        obj is EquipmentStatisticsModifiers other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = PhysicalDefense;
            hash = (hash * 397) ^ MagicalDefense;
            hash = (hash * 397) ^ MaximumHealthModifier;
            hash = (hash * 397) ^ MaximumStaminaModifier;
            hash = (hash * 397) ^ MaximumManaModifier;
            return (hash * 397) ^ KnockbackResistance;
        }
    }
}
