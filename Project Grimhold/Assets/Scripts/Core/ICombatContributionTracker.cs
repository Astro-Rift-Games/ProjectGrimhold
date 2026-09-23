using System.Collections.Generic;

/// <summary>
/// Authoritatively tracks valid combat contributions over a configurable time window.
/// Used to determine assist eligibility when the entity is eliminated.
/// </summary>
public interface ICombatContributionTracker : IEntity
{
    /// <summary>
    /// Records a valid contribution by the given attacker participant.
    /// Replaces the previous timestamp if the attacker already contributed.
    /// </summary>
    void TryRecordContribution(RaidParticipantId contributor, int simulationTick);

    /// <summary>
    /// Retrieves a snapshot of all participant IDs whose latest contribution 
    /// occurred within the acceptable time window relative to <paramref name="currentTick"/>.
    /// </summary>
    void GetValidContributors(int currentTick, HashSet<RaidParticipantId> contributors);
}
