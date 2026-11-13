/// <summary>What a connected Client does on one poll of its Raid admission wait.</summary>
public enum RaidAdmissionWaitDecision
{
    Continue,
    Admitted,
    Abort
}

/// <summary>Time-dependent state of the Client admission wait; the default value is "nothing observed yet".</summary>
public struct RaidAdmissionWaitState
{
    internal bool IsClosedWithoutParticipant;
    internal float ClosedWithoutParticipantSince;
}

/// <summary>
/// Pure rules for the Client side of the Raid admission wait (FIX-03). The Client leaves within one poll
/// when the replicated match shows that admission closed or aborted, instead of waiting out
/// <see cref="RaidAdmissionWindows.ClientAdmissionSeconds"/>. A <c>BootstrapFailure</c> closure aborts at once; a
/// phase that merely moved on without a local participant aborts after
/// <see cref="RaidAdmissionWindows.ClientAdmissionClosedGraceSeconds"/> of continuous absence.
/// </summary>
public static class RaidAdmissionWaitPolicy
{
    /// <param name="state">Caller-owned state carried across polls of one wait.</param>
    /// <param name="now">Monotonic time of this poll, in seconds.</param>
    /// <param name="localAdmitted">The local participant and loadout already match the reservation.</param>
    /// <param name="localParticipantPresent">The Host already replicated a participant for the local player.</param>
    /// <param name="matchObserved">The match controller is bound and readable on this peer.</param>
    /// <param name="phase">Replicated match phase.</param>
    /// <param name="closureReason">Replicated closure reason.</param>
    public static RaidAdmissionWaitDecision Evaluate(
        ref RaidAdmissionWaitState state,
        float now,
        bool localAdmitted,
        bool localParticipantPresent,
        bool matchObserved,
        NetworkMatchController.MatchPhase phase,
        RaidClosureReason closureReason)
    {
        // Admission is checked first: Starting -> InProgress can happen within one tick of the last admission.
        if (localAdmitted)
        {
            return RaidAdmissionWaitDecision.Admitted;
        }

        if (!matchObserved)
        {
            state.IsClosedWithoutParticipant = false;
            return RaidAdmissionWaitDecision.Continue;
        }

        // The closure is explicit and definitive: no grace.
        if (closureReason == RaidClosureReason.BootstrapFailure)
        {
            return RaidAdmissionWaitDecision.Abort;
        }

        // Admission closed. A participant already replicated for this Client is still being matched against
        // its reservation (bounded by the client admission window).
        bool closedWithoutParticipant =
            phase != NetworkMatchController.MatchPhase.WaitingForPlayers && !localParticipantPresent;
        if (!closedWithoutParticipant)
        {
            state.IsClosedWithoutParticipant = false;
            return RaidAdmissionWaitDecision.Continue;
        }

        // Fusion does not order the phase and the Client's own participant: tolerate a short, continuous
        // absence before concluding that this Client was not admitted.
        if (!state.IsClosedWithoutParticipant)
        {
            state.IsClosedWithoutParticipant = true;
            state.ClosedWithoutParticipantSince = now;
        }

        return now - state.ClosedWithoutParticipantSince >= RaidAdmissionWindows.ClientAdmissionClosedGraceSeconds
            ? RaidAdmissionWaitDecision.Abort
            : RaidAdmissionWaitDecision.Continue;
    }
}
