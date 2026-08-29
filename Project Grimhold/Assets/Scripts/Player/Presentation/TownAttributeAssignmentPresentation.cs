/// <summary>Immutable Town projection of confirmed character attributes.</summary>
public readonly struct TownAttributeAssignmentPresentation
{
    public int Vitality { get; }
    public int Resistance { get; }
    public int Strength { get; }
    public int Dexterity { get; }
    public int Intelligence { get; }
    public int Luck { get; }
    public int AvailablePoints { get; }

    public bool CanAssignVitality { get; }
    public bool CanAssignResistance { get; }
    public bool CanAssignStrength { get; }
    public bool CanAssignDexterity { get; }
    public bool CanAssignIntelligence { get; }
    public bool CanAssignLuck { get; }

    private TownAttributeAssignmentPresentation(
        in CharacterAttributeState state,
        bool canAssignVitality,
        bool canAssignResistance,
        bool canAssignStrength,
        bool canAssignDexterity,
        bool canAssignIntelligence,
        bool canAssignLuck)
    {
        Vitality = state.Vitality;
        Resistance = state.Resistance;
        Strength = state.Strength;
        Dexterity = state.Dexterity;
        Intelligence = state.Intelligence;
        Luck = state.Luck;
        AvailablePoints = state.AvailablePoints;
        CanAssignVitality = canAssignVitality;
        CanAssignResistance = canAssignResistance;
        CanAssignStrength = canAssignStrength;
        CanAssignDexterity = canAssignDexterity;
        CanAssignIntelligence = canAssignIntelligence;
        CanAssignLuck = canAssignLuck;
    }

    public static bool TryCreate(
        in CharacterAttributeState state,
        int maximumAttributeValue,
        out TownAttributeAssignmentPresentation presentation)
    {
        presentation = default;

        bool canAssignVitality = CanAssign(state, maximumAttributeValue, CharacterAttribute.Vitality, out CharacterAttributeAssignmentFailure failure);
        if (failure == CharacterAttributeAssignmentFailure.InvalidMaximumAttributeValue)
        {
            return false;
        }

        presentation = new TownAttributeAssignmentPresentation(
            state,
            canAssignVitality,
            CanAssign(state, maximumAttributeValue, CharacterAttribute.Resistance, out _),
            CanAssign(state, maximumAttributeValue, CharacterAttribute.Strength, out _),
            CanAssign(state, maximumAttributeValue, CharacterAttribute.Dexterity, out _),
            CanAssign(state, maximumAttributeValue, CharacterAttribute.Intelligence, out _),
            CanAssign(state, maximumAttributeValue, CharacterAttribute.Luck, out _));
        return true;
    }

    public bool TryGet(
        CharacterAttribute attribute,
        out int value,
        out bool canAssign)
    {
        switch (attribute)
        {
            case CharacterAttribute.Vitality:
                value = Vitality;
                canAssign = CanAssignVitality;
                return true;
            case CharacterAttribute.Resistance:
                value = Resistance;
                canAssign = CanAssignResistance;
                return true;
            case CharacterAttribute.Strength:
                value = Strength;
                canAssign = CanAssignStrength;
                return true;
            case CharacterAttribute.Dexterity:
                value = Dexterity;
                canAssign = CanAssignDexterity;
                return true;
            case CharacterAttribute.Intelligence:
                value = Intelligence;
                canAssign = CanAssignIntelligence;
                return true;
            case CharacterAttribute.Luck:
                value = Luck;
                canAssign = CanAssignLuck;
                return true;
            default:
                value = 0;
                canAssign = false;
                return false;
        }
    }

    private static bool CanAssign(
        in CharacterAttributeState state,
        int maximumAttributeValue,
        CharacterAttribute attribute,
        out CharacterAttributeAssignmentFailure failure) =>
        CharacterAttributeAssignmentRules.TryAssign(
            maximumAttributeValue,
            state,
            attribute,
            out _,
            out failure);
}
