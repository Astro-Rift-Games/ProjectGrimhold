/// <summary>Pure mapping from an authoritative participant result to its Results headline.</summary>
public static class RaidResultsOutcomeResolver
{
    public static RaidResultsOutcome Resolve(
        RaidParticipantState state,
        ExpeditionProgressionFinalizationCause finalizationCause)
    {
        return state switch
        {
            RaidParticipantState.Extracted => RaidResultsOutcome.Extracted,
            RaidParticipantState.Defeated => RaidResultsOutcome.Defeated,
            RaidParticipantState.Aborted when finalizationCause ==
                ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed =>
                RaidResultsOutcome.Abandoned,
            _ => RaidResultsOutcome.None
        };
    }
}
