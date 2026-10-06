using System;

/// <summary>One attribute requirement of an ability compared with the character's current value.</summary>
public readonly struct TownAbilityRequirementCheck : IEquatable<TownAbilityRequirementCheck>
{
    public CharacterAttribute Attribute { get; }
    public int Required { get; }
    public int Current { get; }
    public bool IsMet => Current >= Required;

    public TownAbilityRequirementCheck(CharacterAttribute attribute, int required, int current)
    {
        Attribute = attribute;
        Required = required;
        Current = current;
    }

    public bool Equals(TownAbilityRequirementCheck other) =>
        Attribute == other.Attribute && Required == other.Required && Current == other.Current;

    public override bool Equals(object obj) => obj is TownAbilityRequirementCheck other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Attribute, Required, Current);
}
