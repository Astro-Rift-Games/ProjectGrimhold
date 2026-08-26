/// <summary>Pure deterministic rules for applying one resolved consolidated reward once.</summary>
public static class ConsolidatedExperienceApplicationRules
{
    public static bool TryApply(
        ExperienceCurve curve,
        in ConsolidatedExperienceApplication previous,
        int currentLevel,
        long currentExperience,
        in ExpeditionExperienceResolution resolution,
        out ConsolidatedExperienceApplication candidate,
        out ConsolidatedExperienceApplicationFailure failure)
    {
        candidate = previous;
        failure = ConsolidatedExperienceApplicationFailure.None;

        if (previous.IsApplied)
        {
            failure = ConsolidatedExperienceApplicationFailure.AlreadyApplied;
            return false;
        }

        if (!resolution.IsResolved)
        {
            failure = ConsolidatedExperienceApplicationFailure.UnresolvedResolution;
            return false;
        }

        if (!CharacterProgressionRules.IsValidState(curve, currentLevel, currentExperience))
        {
            failure = ConsolidatedExperienceApplicationFailure.InvalidProgressionState;
            return false;
        }

        ExperienceApplicationResult result;
        if (resolution.ConsolidatedExperience == 0)
        {
            result = new ExperienceApplicationResult(
                currentLevel,
                currentExperience,
                currentLevel,
                currentExperience,
                0);
        }
        else if (!CharacterProgressionRules.TryApplyExperience(
                     curve,
                     currentLevel,
                     currentExperience,
                     resolution.ConsolidatedExperience,
                     out result))
        {
            failure = ConsolidatedExperienceApplicationFailure.InvalidProgressionState;
            return false;
        }

        candidate = new ConsolidatedExperienceApplication(result);
        return true;
    }
}
