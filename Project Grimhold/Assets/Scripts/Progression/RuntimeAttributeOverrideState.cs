using System;

/// <summary>
/// Runtime-only offsets composed over a persistent character-attribute snapshot.
/// This value has no persistence or progression behavior.
/// </summary>
public readonly struct RuntimeAttributeOverrideState : IEquatable<RuntimeAttributeOverrideState>
{
    public int Vitality { get; }
    public int Resistance { get; }
    public int Strength { get; }
    public int Dexterity { get; }
    public int Intelligence { get; }
    public int Luck { get; }

    public RuntimeAttributeOverrideState(
        int vitality,
        int resistance,
        int strength,
        int dexterity,
        int intelligence,
        int luck)
    {
        Vitality = vitality;
        Resistance = resistance;
        Strength = strength;
        Dexterity = dexterity;
        Intelligence = intelligence;
        Luck = luck;
    }

    public bool TryApply(
        in CharacterAttributeState persistent,
        out CharacterAttributeState effective)
    {
        return CharacterAttributeState.TryCreate(
            ApplyOffset(persistent.Vitality, Vitality),
            ApplyOffset(persistent.Resistance, Resistance),
            ApplyOffset(persistent.Strength, Strength),
            ApplyOffset(persistent.Dexterity, Dexterity),
            ApplyOffset(persistent.Intelligence, Intelligence),
            ApplyOffset(persistent.Luck, Luck),
            persistent.AvailablePoints,
            out effective);
    }

    public bool TryAdjust(
        CharacterAttribute attribute,
        int amount,
        in CharacterAttributeState persistent,
        out RuntimeAttributeOverrideState result)
    {
        result = this;
        if (amount == 0 || !persistent.TryGetValue(attribute, out int persistentValue) ||
            !TryGetValue(attribute, out int currentOffset))
        {
            return false;
        }

        int effectiveValue = ApplyOffset(persistentValue, currentOffset);
        int adjustedValue = ClampToAttributeRange((long)effectiveValue + amount);
        int adjustedOffset = adjustedValue - persistentValue;
        result = WithValue(attribute, adjustedOffset);
        return true;
    }

    public RuntimeAttributeOverrideState Reset(CharacterAttribute attribute) =>
        WithValue(attribute, 0);

    public RuntimeAttributeOverrideState ResetAll() => default;

    public bool TryGetValue(CharacterAttribute attribute, out int value)
    {
        switch (attribute)
        {
            case CharacterAttribute.Vitality: value = Vitality; return true;
            case CharacterAttribute.Resistance: value = Resistance; return true;
            case CharacterAttribute.Strength: value = Strength; return true;
            case CharacterAttribute.Dexterity: value = Dexterity; return true;
            case CharacterAttribute.Intelligence: value = Intelligence; return true;
            case CharacterAttribute.Luck: value = Luck; return true;
            default: value = 0; return false;
        }
    }

    public bool Equals(RuntimeAttributeOverrideState other) =>
        Vitality == other.Vitality &&
        Resistance == other.Resistance &&
        Strength == other.Strength &&
        Dexterity == other.Dexterity &&
        Intelligence == other.Intelligence &&
        Luck == other.Luck;

    public override bool Equals(object obj) =>
        obj is RuntimeAttributeOverrideState other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Vitality;
            hash = (hash * 397) ^ Resistance;
            hash = (hash * 397) ^ Strength;
            hash = (hash * 397) ^ Dexterity;
            hash = (hash * 397) ^ Intelligence;
            return (hash * 397) ^ Luck;
        }
    }

    private RuntimeAttributeOverrideState WithValue(CharacterAttribute attribute, int value)
    {
        return attribute switch
        {
            CharacterAttribute.Vitality => new RuntimeAttributeOverrideState(
                value, Resistance, Strength, Dexterity, Intelligence, Luck),
            CharacterAttribute.Resistance => new RuntimeAttributeOverrideState(
                Vitality, value, Strength, Dexterity, Intelligence, Luck),
            CharacterAttribute.Strength => new RuntimeAttributeOverrideState(
                Vitality, Resistance, value, Dexterity, Intelligence, Luck),
            CharacterAttribute.Dexterity => new RuntimeAttributeOverrideState(
                Vitality, Resistance, Strength, value, Intelligence, Luck),
            CharacterAttribute.Intelligence => new RuntimeAttributeOverrideState(
                Vitality, Resistance, Strength, Dexterity, value, Luck),
            CharacterAttribute.Luck => new RuntimeAttributeOverrideState(
                Vitality, Resistance, Strength, Dexterity, Intelligence, value),
            _ => this
        };
    }

    private static int ApplyOffset(int persistentValue, int offset) =>
        ClampToAttributeRange((long)persistentValue + offset);

    private static int ClampToAttributeRange(long value) =>
        value <= 0 ? 0 : value >= int.MaxValue ? int.MaxValue : (int)value;
}
