using System;
using UnityEngine;

/// <summary>One authored minimum attribute requirement.</summary>
[Serializable]
public struct CharacterAttributeRequirement : IEquatable<CharacterAttributeRequirement>
{
    [SerializeField]
    private CharacterAttribute _attribute;

    [SerializeField]
    private int _minimumValue;

    public CharacterAttribute Attribute => _attribute;
    public int MinimumValue => _minimumValue;

    public CharacterAttributeRequirement(CharacterAttribute attribute, int minimumValue)
    {
        _attribute = attribute;
        _minimumValue = minimumValue;
    }

    public bool TryValidate(out string error)
    {
        if (!Enum.IsDefined(typeof(CharacterAttribute), _attribute))
        {
            error = $"Unknown character attribute '{_attribute}'.";
            return false;
        }

        if (_minimumValue <= 0)
        {
            error = $"Minimum value for '{_attribute}' must be positive.";
            return false;
        }

        error = null;
        return true;
    }

    public bool IsSatisfiedBy(in CharacterAttributeState attributes) =>
        TryValidate(out _) &&
        attributes.TryGetValue(_attribute, out int value) &&
        value >= _minimumValue;

    public bool Equals(CharacterAttributeRequirement other) =>
        _attribute == other._attribute && _minimumValue == other._minimumValue;

    public override bool Equals(object obj) =>
        obj is CharacterAttributeRequirement other && Equals(other);

    public override int GetHashCode() => ((int)_attribute * 397) ^ _minimumValue;
}
