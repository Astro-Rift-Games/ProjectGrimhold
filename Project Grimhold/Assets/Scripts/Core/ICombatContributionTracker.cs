using System.Collections.Generic;

/// <summary>
/// Authoritatively tracks valid combat contributions over a configurable time window.
/// Used to determine assist eligibility when the entity is eliminated.
/// </summary>
public interface ICombatContributionTracker : IEntity
{
    /// <summary>
    /// Records an already-effective contribution on this target by the given participant.
    /// Replaces the latest kind and timestamp; the window, not Downed-cycle entry, expires history.
    /// </summary>
    void TryRecordContribution(RaidParticipantId contributor, int simulationTick,
        CombatContributionKind kind = CombatContributionKind.Damage);

    /// <summary>
    /// Retrieves a snapshot of all participant IDs whose latest contribution 
    /// occurred within the acceptable time window relative to <paramref name="currentTick"/>.
    /// </summary>
    void GetValidContributors(int currentTick, HashSet<RaidParticipantId> contributors);

    /// <summary>Reads the latest effective contribution's kind and resolution tick for audit.</summary>
    bool TryGetLatestContribution(RaidParticipantId contributor, out int simulationTick, out CombatContributionKind kind);
}
