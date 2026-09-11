using System;
using UnityEngine;

/// <summary>One present attribute scaling modifier owned by a future weapon instance.</summary>
[Serializable]
public struct WeaponScalingModifier
{
    [SerializeField] private CharacterAttribute _attribute;
    [SerializeField] private WeaponScalingGrade _grade;

    public CharacterAttribute Attribute => _attribute;
    public WeaponScalingGrade Grade => _grade;
    public bool IsPresent => _grade != WeaponScalingGrade.None;

    private WeaponScalingModifier(CharacterAttribute attribute, WeaponScalingGrade grade)
    {
        _attribute = attribute;
        _grade = grade;
    }

    public static bool TryCreate(
        CharacterAttribute attribute,
        WeaponScalingGrade grade,
        out WeaponScalingModifier modifier,
        out string error)
    {
        modifier = new WeaponScalingModifier(attribute, grade);
        if (!modifier.TryValidatePresent(out error))
        {
            modifier = default;
            return false;
        }

        return true;
    }

    /// <summary>Canonicalizes Unity's default sentinel as an absent modifier.</summary>
    public WeaponScalingModifier Normalize() => IsPresent ? this : default;

    public bool TryGetCoefficient(out float coefficient)
    {
        coefficient = _grade switch
        {
            WeaponScalingGrade.E => 0.25f,
            WeaponScalingGrade.D => 0.40f,
            WeaponScalingGrade.C => 0.55f,
            WeaponScalingGrade.B => 0.70f,
            WeaponScalingGrade.A => 0.85f,
            WeaponScalingGrade.S => 1.00f,
            _ => 0f
        };

        return _grade >= WeaponScalingGrade.E && _grade <= WeaponScalingGrade.S;
    }

    public bool TryValidatePresent(out string error)
    {
        if (!Enum.IsDefined(typeof(CharacterAttribute), _attribute))
        {
            error = $"Weapon scaling modifier has unsupported attribute '{(int)_attribute}'.";
            return false;
        }

        if (!TryGetCoefficient(out _))
        {
            error = $"A present weapon scaling modifier requires a grade from E through S, not '{_grade}'.";
            return false;
        }

        error = null;
        return true;
    }
}
