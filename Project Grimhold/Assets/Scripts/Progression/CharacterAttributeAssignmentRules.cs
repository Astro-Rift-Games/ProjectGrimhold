using System;

/// <summary>Pure deterministic rules for assigning one available point to one character attribute.</summary>
public static class CharacterAttributeAssignmentRules
{
    public static bool TryAssign(
        int maximumAttributeValue,
        in CharacterAttributeState currentState,
        CharacterAttribute attribute,
        out CharacterAttributeState candidate,
        out CharacterAttributeAssignmentFailure failure)
    {
        candidate = currentState;
        failure = CharacterAttributeAssignmentFailure.None;

        if (maximumAttributeValue < 0)
        {
            failure = CharacterAttributeAssignmentFailure.InvalidMaximumAttributeValue;
            return false;
        }

        if (!currentState.TryGetValue(attribute, out int currentValue))
        {
            failure = CharacterAttributeAssignmentFailure.UnknownAttribute;
            return false;
        }

        if (currentState.AvailablePoints == 0)
        {
            failure = CharacterAttributeAssignmentFailure.NoAvailablePoints;
            return false;
        }

        if (currentValue >= maximumAttributeValue)
        {
            failure = CharacterAttributeAssignmentFailure.AttributeAtMaximum;
            return false;
        }

        int vitality = currentState.Vitality;
        int resistance = currentState.Resistance;
        int strength = currentState.Strength;
        int dexterity = currentState.Dexterity;
        int intelligence = currentState.Intelligence;
        int luck = currentState.Luck;

        switch (attribute)
        {
            case CharacterAttribute.Vitality:
                vitality++;
                break;
            case CharacterAttribute.Resistance:
                resistance++;
                break;
            case CharacterAttribute.Strength:
                strength++;
                break;
            case CharacterAttribute.Dexterity:
                dexterity++;
                break;
            case CharacterAttribute.Intelligence:
                intelligence++;
                break;
            case CharacterAttribute.Luck:
                luck++;
                break;
        }

        if (!CharacterAttributeState.TryCreate(
                vitality,
                resistance,
                strength,
                dexterity,
                intelligence,
                luck,
                currentState.AvailablePoints - 1,
                out candidate))
        {
            candidate = currentState;
            throw new InvalidOperationException("Attribute assignment produced a structurally invalid state.");
        }

        return true;
    }
}
