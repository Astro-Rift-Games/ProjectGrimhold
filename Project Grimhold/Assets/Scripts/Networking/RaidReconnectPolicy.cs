using Fusion;

/// <summary>
/// FIX-51: pure decisions for the in-app Raid reconnect proof and its rejection outcome.
/// </summary>
public static class RaidReconnectPolicy
{
    /// <summary>
    /// Builds the ticket kept when the Raid connection is lost unexpectedly. It must carry the
    /// reservation proof (SEC-59.4); the active transition ticket wins over the last persisted ticket.
    /// </summary>
    public static RaidReconnectTicket BuildTicketAtUnexpectedShutdown(
        RaidCode raidCode,
        ProfileId profileId,
        string raidGenerationId,
        string sessionName,
        string activeReservationId,
        string persistedReservationId)
    {
        string reservationId = !string.IsNullOrEmpty(activeReservationId)
            ? activeReservationId
            : persistedReservationId;
        return new RaidReconnectTicket(raidCode, profileId, raidGenerationId, sessionName, reservationId);
    }

    /// <summary>
    /// Reason recorded when the reconnect StartGame succeeded but local admission did not complete.
    /// A runner that ended before the deadline was disconnected by the Host (admission rejected).
    /// </summary>
    public static ShutdownReason ClassifyAdmissionFailure(bool runnerEndedBeforeDeadline)
    {
        return runnerEndedBeforeDeadline ? ShutdownReason.ConnectionRefused : ShutdownReason.Error;
    }

    public static bool ShouldRetryAttempt(ShutdownReason reason, float remainingSeconds)
    {
        return remainingSeconds > 0f &&
               !SessionConnectionCoordinator.IsTerminalReconnectShutdownReason(reason);
    }

    public static string DescribeProof(string reservationId)
    {
        return string.IsNullOrEmpty(reservationId) ? "no" : "yes";
    }
}
