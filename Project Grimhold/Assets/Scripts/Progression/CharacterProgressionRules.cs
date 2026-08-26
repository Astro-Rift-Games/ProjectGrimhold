/// <summary>
/// Pure deterministic rules for applying consolidated experience to a character level.
/// </summary>
public static class CharacterProgressionRules
{
    /// <summary>
    /// Applies a positive experience reward without mutating external state.
    /// </summary>
    public static bool TryApplyExperience(
        ExperienceCurve curve,
        int currentLevel,
        long currentExperience,
        long awardedExperience,
        out ExperienceApplicationResult result)
    {
        result = default;
        if (awardedExperience <= 0 || !IsValidState(curve, currentLevel, currentExperience))
        {
            return false;
        }

        if (currentLevel == curve.MaximumLevel)
        {
            result = new ExperienceApplicationResult(
                currentLevel,
                currentExperience,
                currentLevel,
                currentExperience,
                0);
            return true;
        }

        int resultingLevel = currentLevel;
        long resultingExperience = currentExperience;
        long remainingExperience = awardedExperience;

        while (resultingLevel < curve.MaximumLevel)
        {
            curve.TryGetRequiredExperience(resultingLevel, out long requiredExperience);
            long missingExperience = requiredExperience - resultingExperience;
            if (remainingExperience < missingExperience)
            {
                resultingExperience += remainingExperience;
                break;
            }

            remainingExperience -= missingExperience;
            resultingLevel++;
            resultingExperience = 0;

            if (remainingExperience == 0)
            {
                break;
            }
        }

        if (resultingLevel == curve.MaximumLevel)
        {
            resultingExperience = 0;
        }

        result = new ExperienceApplicationResult(
            currentLevel,
            currentExperience,
            resultingLevel,
            resultingExperience,
            resultingLevel - currentLevel);
        return true;
    }

    internal static bool IsValidState(
        ExperienceCurve curve,
        int currentLevel,
        long currentExperience)
    {
        if (curve == null || currentExperience < 0 ||
            currentLevel < ExperienceCurve.InitialLevel || currentLevel > curve.MaximumLevel)
        {
            return false;
        }

        if (currentLevel == curve.MaximumLevel)
        {
            return currentExperience == 0;
        }

        return curve.TryGetRequiredExperience(currentLevel, out long currentRequirement) &&
               currentExperience < currentRequirement;
    }
}
