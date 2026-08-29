/// <summary>Pure deterministic rules for granting available attribute points from a level transition.</summary>
public static class CharacterAttributePointGrantRules
{
    public static bool TryApply(
        int pointsPerLevel,
        in CharacterAttributePointGrant previous,
        in CharacterAttributeState currentState,
        in ExperienceApplicationResult progressionResult,
        out CharacterAttributePointGrant candidate,
        out CharacterAttributePointGrantFailure failure)
    {
        candidate = previous;
        failure = CharacterAttributePointGrantFailure.None;

        if (previous.IsApplied)
        {
            failure = CharacterAttributePointGrantFailure.AlreadyApplied;
            return false;
        }

        if (pointsPerLevel <= 0)
        {
            failure = CharacterAttributePointGrantFailure.InvalidPointsPerLevel;
            return false;
        }

        if (!IsStructurallyValid(progressionResult))
        {
            failure = CharacterAttributePointGrantFailure.InvalidProgressionResult;
            return false;
        }

        long grantedPoints = (long)progressionResult.LevelsGained * pointsPerLevel;
        long resultingAvailablePoints = currentState.AvailablePoints + grantedPoints;
        if (grantedPoints > int.MaxValue || resultingAvailablePoints > int.MaxValue)
        {
            failure = CharacterAttributePointGrantFailure.AvailablePointsOverflow;
            return false;
        }

        if (!CharacterAttributeState.TryCreate(
                currentState.Vitality,
                currentState.Resistance,
                currentState.Strength,
                currentState.Dexterity,
                currentState.Intelligence,
                currentState.Luck,
                (int)resultingAvailablePoints,
                out CharacterAttributeState resultingState))
        {
            failure = CharacterAttributePointGrantFailure.AvailablePointsOverflow;
            return false;
        }

        candidate = new CharacterAttributePointGrant((int)grantedPoints, resultingState);
        return true;
    }

    private static bool IsStructurallyValid(in ExperienceApplicationResult result) =>
        result.PreviousLevel >= ExperienceCurve.InitialLevel &&
        result.ResultingLevel >= result.PreviousLevel &&
        result.LevelsGained >= 0 &&
        result.LevelsGained == result.ResultingLevel - result.PreviousLevel &&
        result.PreviousExperience >= 0 &&
        result.ResultingExperience >= 0;
}
