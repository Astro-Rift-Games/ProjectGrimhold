using System;

/// <summary>
/// Pure deterministic rules for team extraction sanctuary ritual initiation and cancellation.
/// Adheres to Game Design doc 06 §11, decision D-5, and assumption A-4.
/// </summary>
public static class TeamRitualRules
{
    /// <summary>
    /// Checks whether an individual member can initiate the ritual for their assigned sanctuary.
    /// Initiation requires that the sanctuary is assigned to the member's team, the member is alive,
    /// connected, and NOT in the Downed state (must be fully Active).
    /// </summary>
    public static bool CanInitiateRitual(
        bool isSanctuaryAssignedToTeam,
        bool isMemberAlive,
        bool isMemberDowned,
        bool isMemberConnected)
    {
        return isSanctuaryAssignedToTeam && isMemberAlive && !isMemberDowned && isMemberConnected;
    }

    /// <summary>
    /// Evaluates whether an in-progress team ritual should be cancelled.
    /// Per D-5 and A-4, the ritual is NOT cancelled by movement, damage, Downed state, or disconnection of the initiator.
    /// It is cancelled ONLY if the team no longer retains any required member (all members have reached Definitive Defeat or Abandoned).
    /// </summary>
    /// <param name="remainingRequiredMemberCount">Number of members still Raiding (Active or Downed, connected or disconnected).</param>
    public static bool ShouldCancelRitual(int remainingRequiredMemberCount)
    {
        return remainingRequiredMemberCount <= 0;
    }
}
