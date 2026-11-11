/// <summary>How the replacement Host treats the historical Host in the recovered roster.</summary>
public enum HistoricHostRosterClass
{
    /// <summary>Abrupt loss: the Host profile is unrecovered and finalized by the sealing step.</summary>
    Unresolved,
    /// <summary>The Host already resolved its exit: terminal, never re-finalized or re-aborted.</summary>
    DepartedTerminal
}

/// <summary>Pure roster rules for Host Migration (FIX-50 R2).</summary>
public static class HostMigrationRosterPolicy
{
    /// <summary>
    /// Classifies the historical Host from replicated participant state only. The progression
    /// resolver is local to the outgoing Host and is not restored, so the result snapshot cannot
    /// be observed here; the outgoing Host replicates the resolution as <c>IsReturnAuthorized</c>.
    /// </summary>
    public static HistoricHostRosterClass ClassifyHistoricHost(
        RaidParticipantState state,
        ExpeditionProgressionFinalizationCause finalizationCause,
        bool isReturnAuthorized)
    {
        return HostHandoffPolicy.IsHostExitResolved(
            state,
            finalizationCause,
            isReturnAuthorized,
            hasProgressionResultSnapshot: false)
            ? HistoricHostRosterClass.DepartedTerminal
            : HistoricHostRosterClass.Unresolved;
    }
}
