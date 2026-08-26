/// <summary>Pure deterministic accumulation rules for provisional expedition experience.</summary>
public static class ExpeditionExperienceRules
{
    /// <summary>
    /// Calculates the complete candidate state for one normal Dungeon reward.
    /// Extracted Loot is intentionally reserved for the specialized confirmed-extraction path.
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

    /// <summary>
    /// Calculates the complete candidate for one confirmed extraction reward.
    /// Zero is valid because deterministic percentage flooring may award no Experience.
    /// </summary>
    public static bool TryApplyExtractedLootReward(
        in ExpeditionExperienceSnapshot current,
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

        if (amount < 0)
        {
            failure = ExpeditionExperienceApplicationFailure.InvalidAmount;
            return false;
        }

        if (current.ExtractedLootExperience > long.MaxValue - amount)
        {
            failure = ExpeditionExperienceApplicationFailure.CategoryOverflow;
            return false;
        }

        if (currentTotal > long.MaxValue - amount)
        {
            failure = ExpeditionExperienceApplicationFailure.TotalOverflow;
            return false;
        }

        candidate = new ExpeditionExperienceSnapshot(
            current.KillExperience,
            current.AssistExperience,
            current.ExplorationExperience,
            current.ExtractedLootExperience + amount);
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
