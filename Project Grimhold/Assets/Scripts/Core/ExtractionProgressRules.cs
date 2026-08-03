/// <summary>
/// Pure arithmetic rules for saturating individual extraction progress without overflow.
/// </summary>
public static class ExtractionProgressRules
{
    public static bool TryCalculateNext(
        int currentProgress,
        int quota,
        long contributionAmount,
        out int nextProgress,
        out bool completedQuota)
    {
        nextProgress = currentProgress;
        completedQuota = quota > 0 && currentProgress >= quota;
        if (quota <= 0 || currentProgress < 0 || currentProgress >= quota || contributionAmount <= 0)
        {
            return false;
        }

        long remaining = (long)quota - currentProgress;
        nextProgress = contributionAmount >= remaining
            ? quota
            : currentProgress + (int)contributionAmount;
        completedQuota = nextProgress >= quota;
        return true;
    }
}
