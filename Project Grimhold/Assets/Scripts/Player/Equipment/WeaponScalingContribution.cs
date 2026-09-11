using System;

/// <summary>One resolved runtime scaling contribution independent from its source format.</summary>
public readonly struct WeaponScalingContribution : IEquatable<WeaponScalingContribution>
{
    public CharacterAttribute Attribute { get; }
    public float Coefficient { get; }
    public bool IsPresent => Coefficient > 0f;

    private WeaponScalingContribution(CharacterAttribute attribute, float coefficient)
    {
        Attribute = attribute;
        Coefficient = coefficient;
    }

    public static bool TryCreate(
        CharacterAttribute attribute,
        float coefficient,
        out WeaponScalingContribution contribution)
    {
        contribution = default;
        if (!Enum.IsDefined(typeof(CharacterAttribute), attribute) ||
            float.IsNaN(coefficient) || float.IsInfinity(coefficient) || coefficient <= 0f)
        {
            return false;
        }

        contribution = new WeaponScalingContribution(attribute, coefficient);
        return true;
    }

    public bool Equals(WeaponScalingContribution other) =>
        Attribute == other.Attribute && Coefficient.Equals(other.Coefficient);

    public override bool Equals(object obj) =>
        obj is WeaponScalingContribution other && Equals(other);

    public override int GetHashCode() => ((int)Attribute * 397) ^ Coefficient.GetHashCode();
}
