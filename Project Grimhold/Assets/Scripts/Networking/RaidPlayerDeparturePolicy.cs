/// <summary>
/// Defines the action the Host should take when a Raid participant disconnects.
/// </summary>
public enum PlayerDepartureAction
{
    RetainDisconnected,
    FinalizeDefinitiveDisconnect,
    FinalizeDefeated,
    FinalizeExtracted,
    FinalizeAborted
}

/// <summary>
/// Pure decisions for how the Host treats a Raid participant whose peer disconnected.
/// </summary>
public static class RaidPlayerDeparturePolicy
{
    /// <summary>
    /// A disconnect does not pause or resolve Downed: a Raiding participant whose avatar is Downed
    /// keeps its objects so the drain can still reach definitive Defeat on the Host.
    /// </summary>
    public static bool ShouldRetainDownedRaider(RaidParticipantState state, bool avatarIsDowned) =>
        state == RaidParticipantState.Raiding && avatarIsDowned;

    /// <summary>
    /// A retained Downed raider stays tracked only while it is still Raiding; once Defeat (or any
    /// other terminal transition) is recorded the normal terminal handling owns the participant.
    /// </summary>
    public static bool IsRetainedDownedRaiderResolved(RaidParticipantState state) =>
        state != RaidParticipantState.Raiding;

    /// <summary>
    /// Evaluates what action the Host should take when a peer disconnects.
    /// </summary>
    public static PlayerDepartureAction EvaluateDeparture(
        RaidParticipantState state,
        bool avatarIsDowned,
        bool configValid)
    {
        if (state == RaidParticipantState.Extracted) return PlayerDepartureAction.FinalizeExtracted;
        if (state == RaidParticipantState.Defeated) return PlayerDepartureAction.FinalizeDefeated;
        if (state == RaidParticipantState.Aborted) return PlayerDepartureAction.FinalizeAborted;

        if (state == RaidParticipantState.Raiding)
        {
            if (configValid)
            {
                return PlayerDepartureAction.RetainDisconnected;
            }

            return PlayerDepartureAction.FinalizeDefinitiveDisconnect;
        }

        return PlayerDepartureAction.FinalizeDefinitiveDisconnect;
    }
}
