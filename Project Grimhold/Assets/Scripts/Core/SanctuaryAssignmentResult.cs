/// <summary>
/// Immutable outcome of a sanctuary assignment query or authoritative reservation attempt.
/// </summary>
public readonly struct SanctuaryAssignmentResult
{
    public bool Success { get; }
    public RaidTeamId TeamId { get; }
    public EntityId PlayerId { get; }
    public EntityId SanctuaryId { get; }
    public bool IsExistingAssignment { get; }
    public SanctuaryAssignmentFailureReason FailureReason { get; }

    private SanctuaryAssignmentResult(
        bool success,
        RaidTeamId teamId,
        EntityId playerId,
        EntityId sanctuaryId,
        bool isExistingAssignment,
        SanctuaryAssignmentFailureReason failureReason)
    {
        Success = success;
        TeamId = teamId;
        PlayerId = playerId;
        SanctuaryId = sanctuaryId;
        IsExistingAssignment = isExistingAssignment;
        FailureReason = failureReason;
    }

    /// <summary>Creates a successful new or idempotently resolved assignment for a team.</summary>
    public static SanctuaryAssignmentResult Assigned(
        RaidTeamId teamId,
        EntityId sanctuaryId,
        bool isExistingAssignment)
    {
        return new SanctuaryAssignmentResult(
            true,
            teamId,
            default,
            sanctuaryId,
            isExistingAssignment,
            SanctuaryAssignmentFailureReason.None);
    }

    /// <summary>Creates a failed assignment result for a team without a sanctuary identity.</summary>
    public static SanctuaryAssignmentResult Rejected(
        RaidTeamId teamId,
        SanctuaryAssignmentFailureReason failureReason)
    {
        return new SanctuaryAssignmentResult(false, teamId, default, default, false, failureReason);
    }

    /// <summary>Creates a successful new or idempotently resolved assignment for an individual player.</summary>
    public static SanctuaryAssignmentResult Assigned(
        EntityId playerId,
        EntityId sanctuaryId,
        bool isExistingAssignment)
    {
        return new SanctuaryAssignmentResult(
            true,
            default,
            playerId,
            sanctuaryId,
            isExistingAssignment,
            SanctuaryAssignmentFailureReason.None);
    }

    /// <summary>Creates a failed assignment result for an individual player without a sanctuary identity.</summary>
    public static SanctuaryAssignmentResult Rejected(
        EntityId playerId,
        SanctuaryAssignmentFailureReason failureReason)
    {
        return new SanctuaryAssignmentResult(false, default, playerId, default, false, failureReason);
    }
}
