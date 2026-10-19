/// <summary>
/// Decides when the level-up celebration plays. The Results view refreshes many times per
/// snapshot, so the celebration is a one-shot latch owned by the caller.
/// </summary>
public static class ResultsLevelUpFeedbackPolicy
{
    public static bool ShouldCelebrate(int levelsGained, bool alreadyCelebrated) =>
        levelsGained > 0 && !alreadyCelebrated;
}
