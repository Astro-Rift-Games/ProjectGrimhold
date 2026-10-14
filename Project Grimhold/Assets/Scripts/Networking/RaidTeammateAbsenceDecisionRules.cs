public enum TeammateAbsenceDecisionResult
{
    Confirmable,
    RejectedParticipantNotRaiding,
    RejectedAlreadyResolved,
    RejectedNotTeammate,
    RejectedConfirmerUnavailable,
    RejectedReconnected,
    RejectedBudgetNotExhausted
}

public static class RaidTeammateAbsenceDecisionRules
{
    public static TeammateAbsenceDecisionResult Evaluate(
        RaidConnectivitySnapshot targetSnapshot,
        bool isTeammate,
        bool isConfirmerConnected,
        RaidParticipantState confirmerState)
    {
        if (targetSnapshot.State != RaidParticipantState.Raiding)
        {
            return TeammateAbsenceDecisionResult.RejectedAlreadyResolved;
        }

        if (!isTeammate)
        {
            return TeammateAbsenceDecisionResult.RejectedNotTeammate;
        }

        if (!isConfirmerConnected || confirmerState != RaidParticipantState.Raiding)
        {
            return TeammateAbsenceDecisionResult.RejectedConfirmerUnavailable;
        }

        if (!targetSnapshot.IsDisconnected)
        {
            return TeammateAbsenceDecisionResult.RejectedReconnected;
        }

        if (!targetSnapshot.IsBudgetExhausted)
        {
            return TeammateAbsenceDecisionResult.RejectedBudgetNotExhausted;
        }

        return TeammateAbsenceDecisionResult.Confirmable;
    }

    public static bool IsEligibleDecider(bool isConnected, RaidParticipantState state)
    {
        return isConnected && state == RaidParticipantState.Raiding;
    }
}
