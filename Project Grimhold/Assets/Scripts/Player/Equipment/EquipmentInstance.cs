using System;

/// <summary>Immutable Town/cache identity. Only the backend issues IDs; legacy cache entries have no ID.</summary>
public sealed class EquipmentInstance : IEquatable<EquipmentInstance>
{
    public string InstanceId { get; }
    public WeaponInstanceModifiers? Modifiers { get; }
    public bool HasIdentity => IsValidId(InstanceId);

    public EquipmentInstance(string instanceId, WeaponInstanceModifiers? modifiers = null)
    {
        InstanceId = instanceId;
        Modifiers = modifiers?.Normalize();
    }

    public static bool IsValidId(string value)
    {
        if (value == null || value.Length != 24) return false;
        foreach (char character in value)
            if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f')) return false;
        return true;
    }

    public static bool IsEquipment(LootDefinition definition) => definition != null &&
        (definition.Category == LootCategory.Weapon || definition.Category == LootCategory.Shield ||
         definition.Category == LootCategory.Helmet || definition.Category == LootCategory.Armor ||
         definition.Category == LootCategory.Gloves || definition.Category == LootCategory.Boots);

    public static EquipmentInstance PlainCache(LootDefinition definition)
    {
        if (definition.Category != LootCategory.Weapon) return new EquipmentInstance(null);
        WeaponDefinition weapon = definition.WeaponDefinition;
        WeaponScalingModifier primary = default;
        if (weapon != null && weapon.OffensiveScaling.HasScaling)
        {
            for (WeaponScalingGrade grade = WeaponScalingGrade.E; grade <= WeaponScalingGrade.S; grade++)
            {
                WeaponScalingModifier.TryCreate(weapon.NaturalScalingAttribute, grade, out var scaling, out _);
                scaling.TryGetCoefficient(out float coefficient);
                if (coefficient == weapon.OffensiveScaling.Coefficient) { primary = scaling; break; }
            }
        }
        WeaponInstanceModifiers.TryCreate(primary, default, weapon != null ? weapon.NaturalScalingAttribute : CharacterAttribute.Strength,
            out var modifiers, out _);
        return new EquipmentInstance(null, modifiers);
    }

    public bool Equals(EquipmentInstance other) => other != null &&
        string.Equals(InstanceId, other.InstanceId, StringComparison.Ordinal) && Nullable.Equals(Modifiers, other.Modifiers);
    public override bool Equals(object obj) => obj is EquipmentInstance other && Equals(other);
    public override int GetHashCode() => (InstanceId?.GetHashCode() ?? 0) ^ (Modifiers?.GetHashCode() ?? 0);
}
