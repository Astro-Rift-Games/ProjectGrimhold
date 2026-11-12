/// <summary>How the replacement Host treats a restored non-Host participant (FIX-51).</summary>
public enum HostMigrationParticipantDisposition
{
    /// <summary>The participant must reconnect during the recovery window.</summary>
    Recover,
    /// <summary>A disconnected Raider with budget left: kept (budget paused) across the handoff.</summary>
    Retain,
    /// <summary>Terminal: never recovered, never retained.</summary>
    Finalize
}

/// <summary>What sealing does with an Extracted participant nobody rebound.</summary>
public enum HostMigrationUnrecoveredSealAction
{
    /// <summary>Existing path: abort/despawn avatar and participant.</summary>
    Finalize,
    /// <summary>Pending extraction commit: keep participant and avatar, clear authority, mark terminal.</summary>
    KeepObjectsTerminal
}

/// <summary>Pure retention rules for Host Migration (FIX-51).</summary>
public static class HostMigrationRetentionPolicy
{
    /// <summary>An Extracted participant whose extraction commit has not completed yet.</summary>
    public static bool IsExtractionCommitPending(
        RaidParticipantState state,
        ExtractionExperienceTransactionPhase phase) =>
        state == RaidParticipantState.Extracted &&
        phase != ExtractionExperienceTransactionPhase.Complete;

    /// <summary>
    /// Decides, from replicated state only, how the replacement Host treats a restored non-Host
    /// participant. A disconnected Raider with budget left is retained (not finalized at seal);
    /// an exhausted budget or confirmed absence keeps the existing recover-or-finalize path.
    /// </summary>
    public static HostMigrationParticipantDisposition Classify(
        RaidParticipantState state,
        bool isReturnAuthorized,
        bool isDisconnected,
        int remainingBudgetTicks,
        bool isAbsenceConfirmed,
        ExtractionExperienceTransactionPhase extractionPhase)
    {
        if (isReturnAuthorized)
        {
            return HostMigrationParticipantDisposition.Finalize;
        }

        if (state == RaidParticipantState.Raiding)
        {
            return isDisconnected && remainingBudgetTicks > 0 && !isAbsenceConfirmed
                ? HostMigrationParticipantDisposition.Retain
                : HostMigrationParticipantDisposition.Recover;
        }

        return state == RaidParticipantState.Defeated ||
               IsExtractionCommitPending(state, extractionPhase)
            ? HostMigrationParticipantDisposition.Recover
            : HostMigrationParticipantDisposition.Finalize;
    }

    /// <summary>
    /// A profile rebound during the window stays valid at seal while it is Raiding, Defeated or
    /// Extracted (its commit may complete before sealing), unless Return was authorized.
    /// </summary>
    public static bool IsRecoveredStateValid(RaidParticipantState state, bool isReturnAuthorized) =>
        !isReturnAuthorized &&
        (state == RaidParticipantState.Raiding ||
         state == RaidParticipantState.Defeated ||
         state == RaidParticipantState.Extracted);

    /// <summary>
    /// An unrecovered Extracted participant with a pending commit keeps its objects (as the
    /// non-migration disconnect path does); the historical Host keeps the existing finalization.
    /// </summary>
    public static HostMigrationUnrecoveredSealAction ClassifyUnrecoveredAtSeal(
        RaidParticipantState state,
        ExtractionExperienceTransactionPhase extractionPhase,
        bool isHistoricHost) =>
        !isHistoricHost && IsExtractionCommitPending(state, extractionPhase)
            ? HostMigrationUnrecoveredSealAction.KeepObjectsTerminal
            : HostMigrationUnrecoveredSealAction.Finalize;

    /// <summary>The reconnect budget does not tick until the recovery roster is sealed.</summary>
    public static bool IsReconnectBudgetPaused(bool isMigrationResume, bool isRecoverySealed) =>
        isMigrationResume && !isRecoverySealed;

    /// <summary>
    /// Arrivals follow the migration-specific boundary until the roster is sealed; afterwards the
    /// normal reconnect path serves retained participants.
    /// </summary>
    public static bool UsesMigrationArrivalBoundary(bool isMigrationResume, bool isRecoverySealed) =>
        isMigrationResume && !isRecoverySealed;
}
