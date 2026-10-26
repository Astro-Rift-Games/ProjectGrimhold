/// <summary>
/// Absolute closure resolves every unresolved raider. Connectivity and avatar Downed state
/// do not exempt a participant; terminal outcomes must never be resolved a second time.
/// </summary>
public static class AbsoluteClosureRules
{
    public static bool ShouldDefeat(RaidParticipantState state) => state == RaidParticipantState.Raiding;
}
