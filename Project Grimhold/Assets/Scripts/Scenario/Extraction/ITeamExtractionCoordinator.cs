using Fusion;

/// <summary>
/// Capability contract for authoritative team extraction coordination.
/// Manages joint team extraction countdowns, composition requirements, and simultaneous completion.
/// </summary>
public interface ITeamExtractionCoordinator : IEntity
{
    /// <summary>
    /// Gets the immutable extraction configuration governing countdown durations and boundary tolerances.
    /// </summary>
    ExtractionConfig Config { get; }

    /// <summary>
    /// Attempts to query the current extraction countdown snapshot for the specified team.
    /// </summary>
    /// <param name="teamId">Target team identity.</param>
    /// <param name="snapshot">Outputs the snapshot if queried; otherwise, default.</param>
    /// <returns><see langword="true"/> if the team was resolved; otherwise, <see langword="false"/>.</returns>
    bool TryGetCountdownProgress(RaidTeamId teamId, out ExtractionCountdownSnapshot snapshot);

    /// <summary>
    /// Returns whether the specified team currently has an active extraction countdown running.
    /// </summary>
    /// <param name="teamId">Target team identity.</param>
    bool IsTeamExtracting(RaidTeamId teamId);
}
