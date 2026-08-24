/// <summary>Pure deterministic accumulation rules for provisional expedition experience.</summary>
public static class ExpeditionExperienceRules
{
    /// <summary>
    /// Calculates the complete candidate state for one normal Dungeon reward.
    /// Extracted Loot is intentionally reserved for the later extraction integration.
    /// </summary>
    public static bool TryApplyNormalReward(
        in ExpeditionExperienceSnapshot current,
        ExpeditionExperienceCategory category,
        long amount,
        out ExpeditionExperienceSnapshot candidate,
        out ExpeditionExperienceApplicationFailure failure)
    {
        candidate = current;
        failure = ExpeditionExperienceApplicationFailure.None;

        if (!TryCalculateTotal(current, out long currentTotal))
        {
            failure = ExpeditionExperienceApplicationFailure.InvalidState;
            return false;
        }

        if (amount <= 0)
        {
            failure = ExpeditionExperienceApplicationFailure.InvalidAmount;
            return false;
        }

        long categoryExperience;
        switch (category)
        {
            case ExpeditionExperienceCategory.Kill:
                categoryExperience = current.KillExperience;
                break;
            case ExpeditionExperienceCategory.Assist:
                categoryExperience = current.AssistExperience;
                break;
            case ExpeditionExperienceCategory.Exploration:
                categoryExperience = current.ExplorationExperience;
                break;
            case ExpeditionExperienceCategory.ExtractedLoot:
                failure = ExpeditionExperienceApplicationFailure.ExtractedLootRequiresExtractionResolution;
                return false;
            default:
                failure = ExpeditionExperienceApplicationFailure.InvalidCategory;
                return false;
        }

        if (categoryExperience > long.MaxValue - amount)
        {
            failure = ExpeditionExperienceApplicationFailure.CategoryOverflow;
            return false;
        }

        if (currentTotal > long.MaxValue - amount)
        {
            failure = ExpeditionExperienceApplicationFailure.TotalOverflow;
            return false;
        }

        long resultingCategoryExperience = categoryExperience + amount;
        candidate = category switch
        {
            ExpeditionExperienceCategory.Kill => new ExpeditionExperienceSnapshot(
                resultingCategoryExperience,
                current.AssistExperience,
                current.ExplorationExperience,
                current.ExtractedLootExperience),
            ExpeditionExperienceCategory.Assist => new ExpeditionExperienceSnapshot(
                current.KillExperience,
                resultingCategoryExperience,
                current.ExplorationExperience,
                current.ExtractedLootExperience),
            _ => new ExpeditionExperienceSnapshot(
                current.KillExperience,
                current.AssistExperience,
                resultingCategoryExperience,
                current.ExtractedLootExperience)
        };
        return true;
    }

    private static bool TryCalculateTotal(
        in ExpeditionExperienceSnapshot snapshot,
        out long total)
    {
        total = 0;
        if (snapshot.KillExperience < 0 || snapshot.AssistExperience < 0 ||
            snapshot.ExplorationExperience < 0 || snapshot.ExtractedLootExperience < 0)
        {
            return false;
        }

        if (snapshot.KillExperience > long.MaxValue - snapshot.AssistExperience)
        {
            return false;
        }

        total = snapshot.KillExperience + snapshot.AssistExperience;
        if (total > long.MaxValue - snapshot.ExplorationExperience)
        {
            total = 0;
            return false;
        }

        total += snapshot.ExplorationExperience;
        if (total > long.MaxValue - snapshot.ExtractedLootExperience)
        {
            total = 0;
            return false;
        }

        total += snapshot.ExtractedLootExperience;
        return true;
    }
}
