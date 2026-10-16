using Fusion;

/// <summary>
/// Authoritative capability interface for team extraction progress.
/// Replaces individual player progress storage with a shared per-team quota pool.
/// </summary>
public interface ITeamExtractionProgress : IEntity
{
    /// <summary>
    /// Attempts to query the current progress and immutable quota for the specified team.
    /// </summary>
    bool TryGetProgress(RaidTeamId teamId, out int currentProgress, out int quota);

    /// <summary>
    /// Attempts to obtain an immutable snapshot of the specified team's extraction progress.
    /// </summary>
    bool TryGetSnapshot(RaidTeamId teamId, out ExtractionProgressSnapshot snapshot);

    /// <summary>
    /// Applies a contribution toward the specified team's quota.
    /// Only the State Authority can apply contributions during an active simulation tick.
    /// </summary>
    bool TryApplyContribution(RaidTeamId teamId, long amount, out int nextProgress, out bool completedQuota);

    /// <summary>
    /// Initializes team slots from the frozen launch roster.
    /// </summary>
    bool TryInitializeFromRoster(RaidTeamRoster roster);

    /// <summary>
    /// Returns whether the team has an initialized slot in this progress tracker.
    /// </summary>
    bool IsAssignedToTeam(RaidTeamId teamId);
}
