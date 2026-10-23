/// <summary>Pure guards for the pre-launch consent phase; they never reserve or grant inventory.</summary>
public static class RecoveryWeaponPreparationLaunchRules
{
    public static bool CanAcceptResponse(bool awaitingConsent, int currentRevision, int responseRevision, bool isMember) =>
        awaitingConsent && currentRevision > 0 && currentRevision == responseRevision && isMember;

    public static bool CanFreeze(int expectedMembershipRevision, int currentMembershipRevision, int memberCount, int acceptedCount) =>
        expectedMembershipRevision == currentMembershipRevision && memberCount > 0 &&
        memberCount <= TownRaidPreparationRules.MaxMembers && acceptedCount == memberCount;
}
