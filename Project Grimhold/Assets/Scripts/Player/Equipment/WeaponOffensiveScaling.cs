using System;
using UnityEngine;

/// <summary>Static offensive attribute scaling configured by one weapon definition.</summary>
[Serializable]
public struct WeaponOffensiveScaling
{
    [SerializeField] private CharacterAttribute _attribute;
    [SerializeField, Min(0f)] private float _coefficient;

    public CharacterAttribute Attribute => _attribute;
    public float Coefficient => _coefficient;
    public bool HasScaling => _coefficient > 0f;

    public WeaponOffensiveScaling(CharacterAttribute attribute, float coefficient)
    {
        _attribute = attribute;
        _coefficient = coefficient;
    }

    public bool TryValidate(out string error)
    {
        if (float.IsNaN(_coefficient) || float.IsInfinity(_coefficient) || _coefficient < 0f)
        {
            error = "Weapon offensive scaling coefficient must be finite and non-negative.";
            return false;
        }

        if (!HasScaling)
        {
            error = null;
            return true;
        }

        if (_attribute != CharacterAttribute.Strength &&
            _attribute != CharacterAttribute.Dexterity &&
            _attribute != CharacterAttribute.Intelligence)
        {
            error = $"Weapon offensive scaling attribute '{_attribute}' is not offensive.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Resolves the selected confirmed attribute, or zero when this configuration has no scaling.
    /// </summary>
    public bool TryResolveAttributeValue(in CharacterAttributeState attributes, out int value)
    {
        value = 0;
        if (!TryValidate(out _))
        {
            return false;
        }

        return !HasScaling || attributes.TryGetValue(_attribute, out value);
    }
}
