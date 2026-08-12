/// <summary>Pure validation for one peer's individual token against a frozen Raid cohort.</summary>
public static class RaidAdmissionRules
{
    public static bool IsAdmitted(RaidLaunchContext launchContext, in RaidAdmissionData admission)
    {
        return launchContext != null && admission.IsValid && admission.RaidCode.IsValid &&
               admission.RaidCode == launchContext.RaidCode &&
               RaidSessionRules.ContainsProfile(
                   launchContext.ParticipantProfileIds,
                   admission.ProfileId);
    }
}
