/// <summary>Pure rules deciding when a technical Raid failure must be reported in Town.</summary>
public static class RaidLossNoticePolicy
{
    /// <summary>
    /// Maps an authoritative closure to its Town notice. Natural completion is not a technical
    /// loss: Abandon, Defeat and Extraction already show their Results inside the Raid.
    /// </summary>
    public static RaidLossNoticeReason? ReasonForClosure(RaidClosureReason closureReason) =>
        closureReason == RaidClosureReason.BootstrapFailure
            ? RaidLossNoticeReason.RaidStartFailed
            : null;

    /// <summary>
    /// A bootstrap or admission-deadline closure aborts every participant without a result, so there
    /// is no Results screen or accepted terminal result to Return from. The Host leaves on its own.
    /// </summary>
    public static bool ShouldReturnHostAfterTechnicalClosure(
        bool isServer,
        bool isRaidState,
        NetworkMatchController.MatchPhase phase,
        RaidClosureReason closureReason)
    {
        return isServer &&
               isRaidState &&
               ReasonForClosure(closureReason).HasValue &&
               (phase == NetworkMatchController.MatchPhase.Closing ||
                phase == NetworkMatchController.MatchPhase.Finished);
    }

    /// <summary>
    /// An unexpected runner shutdown is a reportable loss unless a reconnect ticket keeps the
    /// participation alive or an outgoing Host handoff already showed accepted Results.
    /// </summary>
    public static bool ShouldReportConnectionLoss(bool isOutgoingHostHandoff, bool participationRetained) =>
        !isOutgoingHostHandoff && !participationRetained;
}
