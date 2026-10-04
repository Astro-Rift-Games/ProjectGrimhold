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
}
