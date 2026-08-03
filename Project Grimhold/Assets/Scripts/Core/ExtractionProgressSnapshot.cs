/// <summary>
/// Immutable read-only projection of a player's individual extraction progress.
/// </summary>
public readonly struct ExtractionProgressSnapshot
{
    public int CurrentProgress { get; }
    public int Quota { get; }
    public float Percentage { get; }
    public bool IsQuotaComplete { get; }
    public bool AssignmentRequested { get; }

    public ExtractionProgressSnapshot(
        int currentProgress,
        int quota,
        bool assignmentRequested)
    {
        Quota = UnityEngine.Mathf.Max(0, quota);
        CurrentProgress = UnityEngine.Mathf.Clamp(currentProgress, 0, Quota);
        Percentage = Quota > 0 ? 100f * CurrentProgress / Quota : 0f;
        IsQuotaComplete = Quota > 0 && CurrentProgress >= Quota;
        AssignmentRequested = assignmentRequested;
    }
}
