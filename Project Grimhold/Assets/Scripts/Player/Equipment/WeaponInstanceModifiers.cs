using System;
using UnityEngine;

/// <summary>Canonical optional scaling slots owned by a future unique weapon instance.</summary>
[Serializable]
public struct WeaponInstanceModifiers
{
    [SerializeField] private WeaponScalingModifier _primary;
    [SerializeField] private WeaponScalingModifier _secondary;

    public WeaponScalingModifier Primary => _primary.Normalize();
    public WeaponScalingModifier Secondary => _secondary.Normalize();
    public bool HasPrimary => _primary.IsPresent;
    public bool HasSecondary => _secondary.IsPresent;

    private WeaponInstanceModifiers(
        WeaponScalingModifier primary,
        WeaponScalingModifier secondary)
    {
        _primary = primary.Normalize();
        _secondary = secondary.Normalize();
    }

    public static bool TryCreate(
        WeaponScalingModifier primary,
        WeaponScalingModifier secondary,
        CharacterAttribute naturalAttribute,
        out WeaponInstanceModifiers modifiers,
        out string error)
    {
        modifiers = new WeaponInstanceModifiers(primary, secondary);
        if (!modifiers.TryValidate(naturalAttribute, out error))
        {
            modifiers = default;
            return false;
        }

        return true;
    }

    /// <summary>Removes ignored attributes from absent serialized slots.</summary>
    public WeaponInstanceModifiers Normalize() => new(_primary.Normalize(), _secondary.Normalize());

    public bool TryValidate(CharacterAttribute naturalAttribute, out string error)
    {
        WeaponScalingModifier primary = _primary.Normalize();
        WeaponScalingModifier secondary = _secondary.Normalize();

        if (primary.IsPresent)
        {
            if (!primary.TryValidatePresent(out error))
            {
                return false;
            }

            if (primary.Attribute != naturalAttribute)
            {
                error = $"Primary weapon scaling attribute '{primary.Attribute}' must match natural attribute '{naturalAttribute}'.";
                return false;
            }
        }

        if (!secondary.IsPresent)
        {
            error = null;
            return true;
        }

        if (!primary.IsPresent)
        {
            error = "Secondary weapon scaling cannot exist without a primary scaling modifier.";
            return false;
        }

        if (!secondary.TryValidatePresent(out error))
        {
            return false;
        }

        if (secondary.Attribute == primary.Attribute)
        {
            error = "Primary and secondary weapon scaling modifiers cannot use the same attribute.";
            return false;
        }

        error = null;
        return true;
    }
}
