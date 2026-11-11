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
    /// Classifies the historical Host from replicated participant state only. A Host that confirmed
    /// a voluntary Abandon already generated its corpse (Aborted), so it is terminal even if it
    /// dropped before pressing Return; otherwise the outgoing Host replicates its resolution as
    /// <c>IsReturnAuthorized</c>. The progression resolver is local to the outgoing Host.
    /// </summary>
    public static HistoricHostRosterClass ClassifyHistoricHost(
        RaidParticipantState state,
        ExpeditionProgressionFinalizationCause finalizationCause,
        bool isReturnAuthorized)
    {
        bool abandoned = state == RaidParticipantState.Aborted &&
            finalizationCause == ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed;
        return isReturnAuthorized || abandoned
            ? HistoricHostRosterClass.DepartedTerminal
            : HistoricHostRosterClass.Unresolved;
    }
}
