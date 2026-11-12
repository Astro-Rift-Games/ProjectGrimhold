/// <summary>
/// Pure eligibility rules for leaving an already resolved Raid Results state.
/// </summary>
public static class RaidResultsReturnPolicy
{
    public static bool IsAcceptedTerminalResult(
        RaidParticipantState state,
        ExpeditionProgressionFinalizationCause finalizationCause,
        bool isExtractionProgressionComplete)
    {
        return state switch
        {
            RaidParticipantState.Defeated => true,
            RaidParticipantState.Extracted => isExtractionProgressionComplete,
            RaidParticipantState.Aborted => finalizationCause ==
                ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed,
            _ => false
        };
    }

    public static bool CanRequestClientReturn(
        RaidParticipantState state,
        ExpeditionProgressionFinalizationCause finalizationCause,
        bool isExtractionProgressionComplete,
        bool hasProgressionResultSnapshot,
        bool isCompatiblePhase)
    {
        return isCompatiblePhase &&
               hasProgressionResultSnapshot &&
               IsAcceptedTerminalResult(
                   state,
                   finalizationCause,
                   isExtractionProgressionComplete);
    }

    public static bool CanRequestHostReturn(
        RaidParticipantState state,
        ExpeditionProgressionFinalizationCause finalizationCause,
        bool isExtractionProgressionComplete,
        bool hasProgressionResultSnapshot,
        bool isServer,
        bool hasRaidingParticipants,
        bool isMatchFinished)
    {
        return isServer &&
               !hasRaidingParticipants &&
               isMatchFinished &&
               hasProgressionResultSnapshot &&
               IsAcceptedTerminalResult(
                   state,
                   finalizationCause,
                   isExtractionProgressionComplete);
    }

    /// <summary>
    /// Host Return while the Raid is still in progress: the Host already holds an accepted terminal
    /// result and connected Raiding peers remain, so it hands the Host role off instead of waiting.
    /// </summary>
    public static bool CanRequestHostHandoffReturn(
        RaidParticipantState state,
        ExpeditionProgressionFinalizationCause finalizationCause,
        bool isExtractionProgressionComplete,
        bool hasProgressionResultSnapshot,
        bool isServer,
        bool isMatchInProgress,
        int connectedRaidingRemoteParticipants)
    {
        return isServer &&
               HostHandoffPolicy.ShouldRouteHostReturnThroughParticipant(
                   isMatchInProgress,
                   connectedRaidingRemoteParticipants) &&
               hasProgressionResultSnapshot &&
               IsAcceptedTerminalResult(
                   state,
                   finalizationCause,
                   isExtractionProgressionComplete);
    }

    public static bool ShouldStartHostReturn(
        bool hostReturnRequested,
        bool hostReturnStarted,
        bool operationActive,
        bool coordinatorIsInRaid,
        bool hasValidHostRunner,
        bool isMatchFinished,
        bool hasRaidingParticipants,
        bool hasConnectedRemoteParticipants)
    {
        return hostReturnRequested &&
               !hostReturnStarted &&
               !operationActive &&
               coordinatorIsInRaid &&
               hasValidHostRunner &&
               isMatchFinished &&
               !hasRaidingParticipants &&
               !hasConnectedRemoteParticipants;
    }
}
