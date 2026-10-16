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

    /// <summary>
    /// Attempts to query the count of required team members and how many are currently inside the sanctuary extraction zone and connected.
    /// </summary>
    /// <param name="teamId">Target team identity.</param>
    /// <param name="presentCount">Number of required members currently inside the zone and connected.</param>
    /// <param name="requiredCount">Total number of required members for this team.</param>
    /// <returns><see langword="true"/> if team presence was resolved; otherwise, <see langword="false"/>.</returns>
    bool TryGetTeamPresence(RaidTeamId teamId, out int presentCount, out int requiredCount);
}
