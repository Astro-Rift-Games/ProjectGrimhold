using System.Collections.Generic;

/// <summary>Why the Raid Host aborts a Waiting admission.</summary>
public enum RaidAdmissionAbortDecision
{
    None,

    /// <summary>The Host admission window expired before the frozen cohort was admitted.</summary>
    DeadlineExpired,

    /// <summary>A required member joined the Raid session and left or was rejected before the Raid started.</summary>
    RequiredMemberLost
}

/// <summary>
/// Pure rules deciding when the Raid Host aborts the admission of a coordinated launch with
/// <see cref="RaidClosureReason.BootstrapFailure"/> (FIX-03). The deadline and the loss of a required
/// member share one abort routine; this policy only chooses between them.
/// </summary>
public static class RaidAdmissionAbortPolicy
{
    /// <summary>
    /// A lost member aborts at once and takes precedence, even when the remaining cohort looks complete,
    /// so a Duo expedition never starts as Solo. A member that has not joined yet records no loss and is
    /// still covered by <paramref name="deadlineExpired"/> (<see cref="RaidAdmissionWindows.HostAdmissionSeconds"/>).
    /// </summary>
    public static RaidAdmissionAbortDecision Evaluate(
        NetworkMatchController.MatchPhase phase,
        bool cohortAdmitted,
        bool deadlineExpired,
        bool requiredMemberLost)
    {
        if (phase != NetworkMatchController.MatchPhase.WaitingForPlayers)
        {
            return RaidAdmissionAbortDecision.None;
        }

        if (requiredMemberLost)
        {
            return RaidAdmissionAbortDecision.RequiredMemberLost;
        }

        return deadlineExpired && !cohortAdmitted
            ? RaidAdmissionAbortDecision.DeadlineExpired
            : RaidAdmissionAbortDecision.None;
    }

    /// <summary>
    /// Whether a departed profile is a required remote member of the frozen cohort. The Host profile is
    /// excluded: its own departure ends the session instead of failing a member's entry.
    /// </summary>
    public static bool IsRequiredMemberLoss(
        IReadOnlyList<ProfileId> requiredProfiles,
        ProfileId hostProfile,
        ProfileId departedProfile)
    {
        return departedProfile.IsValid &&
               departedProfile != hostProfile &&
               RaidSessionRules.ContainsProfile(requiredProfiles, departedProfile);
    }
}
