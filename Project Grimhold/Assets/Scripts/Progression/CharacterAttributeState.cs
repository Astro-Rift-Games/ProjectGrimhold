using System;

/// <summary>Immutable structural state of a character's attributes and available points.</summary>
public readonly struct CharacterAttributeState : IEquatable<CharacterAttributeState>
{
    public int Vitality { get; }
    public int Resistance { get; }
    public int Strength { get; }
    public int Dexterity { get; }
    public int Intelligence { get; }
    public int Luck { get; }
    public int AvailablePoints { get; }

    private CharacterAttributeState(
        int vitality,
        int resistance,
        int strength,
        int dexterity,
        int intelligence,
        int luck,
        int availablePoints)
    {
        Vitality = vitality;
        Resistance = resistance;
        Strength = strength;
        Dexterity = dexterity;
        Intelligence = intelligence;
        Luck = luck;
        AvailablePoints = availablePoints;
    }

    /// <summary>Creates a structurally valid state without applying balance limits.</summary>
    public static bool TryCreate(
        int vitality,
        int resistance,
        int strength,
        int dexterity,
        int intelligence,
        int luck,
        int availablePoints,
        out CharacterAttributeState state)
    {
        state = default;
        if (vitality < 0 || resistance < 0 || strength < 0 || dexterity < 0 ||
            intelligence < 0 || luck < 0 || availablePoints < 0)
        {
            return false;
        }

        state = new CharacterAttributeState(
            vitality,
            resistance,
            strength,
            dexterity,
            intelligence,
            luck,
            availablePoints);
        return true;
    }

    /// <summary>Gets the value of one known character attribute.</summary>
    public bool TryGetValue(CharacterAttribute attribute, out int value)
    {
        switch (attribute)
        {
            case CharacterAttribute.Vitality:
                value = Vitality;
                return true;
            case CharacterAttribute.Resistance:
                value = Resistance;
                return true;
            case CharacterAttribute.Strength:
                value = Strength;
                return true;
            case CharacterAttribute.Dexterity:
                value = Dexterity;
                return true;
            case CharacterAttribute.Intelligence:
                value = Intelligence;
                return true;
            case CharacterAttribute.Luck:
                value = Luck;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    public bool Equals(CharacterAttributeState other) =>
        Vitality == other.Vitality &&
        Resistance == other.Resistance &&
        Strength == other.Strength &&
        Dexterity == other.Dexterity &&
        Intelligence == other.Intelligence &&
        Luck == other.Luck &&
        AvailablePoints == other.AvailablePoints;

    public override bool Equals(object obj) =>
        obj is CharacterAttributeState other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Vitality;
            hash = (hash * 397) ^ Resistance;
            hash = (hash * 397) ^ Strength;
            hash = (hash * 397) ^ Dexterity;
            hash = (hash * 397) ^ Intelligence;
            hash = (hash * 397) ^ Luck;
            return (hash * 397) ^ AvailablePoints;
        }
    }
}
