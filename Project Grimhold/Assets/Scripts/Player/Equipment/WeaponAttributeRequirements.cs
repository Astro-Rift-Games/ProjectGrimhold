using System;

/// <summary>
/// Static minimum character attributes required to equip one weapon definition.
/// Zero means that the corresponding attribute is not required.
/// </summary>
[Serializable]
public struct WeaponAttributeRequirements : IEquatable<WeaponAttributeRequirements>
{
    [UnityEngine.SerializeField] private int _minimumStrength;
    [UnityEngine.SerializeField] private int _minimumDexterity;
    [UnityEngine.SerializeField] private int _minimumIntelligence;

    public int MinimumStrength => _minimumStrength;
    public int MinimumDexterity => _minimumDexterity;
    public int MinimumIntelligence => _minimumIntelligence;

    public WeaponAttributeRequirements(
        int minimumStrength,
        int minimumDexterity,
        int minimumIntelligence)
    {
        _minimumStrength = minimumStrength;
        _minimumDexterity = minimumDexterity;
        _minimumIntelligence = minimumIntelligence;
    }

    public bool TryValidate(out string error)
    {
        if (_minimumStrength < 0 || _minimumDexterity < 0 || _minimumIntelligence < 0)
        {
            error = "Weapon attribute requirements cannot be negative.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>Evaluates every configured minimum against one confirmed attribute state.</summary>
    public bool IsSatisfiedBy(in CharacterAttributeState attributes) =>
        TryValidate(out _) &&
        attributes.Strength >= _minimumStrength &&
        attributes.Dexterity >= _minimumDexterity &&
        attributes.Intelligence >= _minimumIntelligence;

    public bool Equals(WeaponAttributeRequirements other) =>
        _minimumStrength == other._minimumStrength &&
        _minimumDexterity == other._minimumDexterity &&
        _minimumIntelligence == other._minimumIntelligence;

    public override bool Equals(object obj) =>
        obj is WeaponAttributeRequirements other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = _minimumStrength;
            hash = (hash * 397) ^ _minimumDexterity;
            return (hash * 397) ^ _minimumIntelligence;
        }
    }
}
