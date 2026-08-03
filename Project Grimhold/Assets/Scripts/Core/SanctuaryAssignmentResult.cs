/// <summary>
/// Immutable outcome of a sanctuary assignment query or authoritative reservation attempt.
/// </summary>
public readonly struct SanctuaryAssignmentResult
{
    public bool Success { get; }
    public EntityId PlayerId { get; }
    public EntityId SanctuaryId { get; }
    public bool IsExistingAssignment { get; }
    public SanctuaryAssignmentFailureReason FailureReason { get; }

    private SanctuaryAssignmentResult(
        bool success,
        EntityId playerId,
        EntityId sanctuaryId,
        bool isExistingAssignment,
        SanctuaryAssignmentFailureReason failureReason)
    {
        Success = success;
        PlayerId = playerId;
        SanctuaryId = sanctuaryId;
        IsExistingAssignment = isExistingAssignment;
        FailureReason = failureReason;
    }

    /// <summary>Creates a successful new or idempotently resolved assignment.</summary>
    public static SanctuaryAssignmentResult Assigned(
        EntityId playerId,
        EntityId sanctuaryId,
        bool isExistingAssignment)
    {
        return new SanctuaryAssignmentResult(
            true,
            playerId,
            sanctuaryId,
            isExistingAssignment,
            SanctuaryAssignmentFailureReason.None);
    }

    /// <summary>Creates a failed assignment result without a sanctuary identity.</summary>
    public static SanctuaryAssignmentResult Rejected(
        EntityId playerId,
        SanctuaryAssignmentFailureReason failureReason)
    {
        return new SanctuaryAssignmentResult(false, playerId, default, false, failureReason);
    }
}
