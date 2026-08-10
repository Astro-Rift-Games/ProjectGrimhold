/// <summary>
/// Pure deterministic rules shared by the Town queue and EditMode tests.
/// </summary>
public static class TownRaidQueueRules
{
    public static bool CanJoin(TownRaidQueueState state, int memberCount, int maximumMembers, bool profileAlreadyPresent)
    {
        return state == TownRaidQueueState.Forming && !profileAlreadyPresent &&
               memberCount >= 0 && maximumMembers >= 1 && memberCount < maximumMembers;
    }

    public static bool CanLaunch(TownRaidQueueState state, bool requesterIsHost, int memberCount, bool allMembersReady)
    {
        return state == TownRaidQueueState.Forming && requesterIsHost && memberCount > 0 && allMembersReady;
    }

    public static bool ShouldDissolveAfterDeparture(TownRaidQueueState state, bool departingMemberIsHost)
    {
        return departingMemberIsHost || state == TownRaidQueueState.Launching;
    }

    public static bool ShouldCancelAfterAuthorityTransfer(TownRaidQueueState state)
    {
        return state == TownRaidQueueState.Launching;
    }
}
