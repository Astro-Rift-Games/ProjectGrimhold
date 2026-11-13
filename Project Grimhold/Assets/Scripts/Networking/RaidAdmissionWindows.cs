/// <summary>
/// Single source of the time budgets that bound a coordinated Raid launch, so the Raid Host and the
/// joining Clients share one coherent limit (FIX-04).
/// </summary>
/// <remarks>
/// Bound for a Client of a Party launch: it is released after the Raid Host left Town (or before the
/// Raid Host leaves when that Host is the Town authority), keeps retrying
/// <c>GameNotFound</c>/<c>GameClosed</c> for at most <see cref="ClientSessionAvailabilitySeconds"/>,
/// and once connected waits at most <see cref="ClientAdmissionSeconds"/> for admission. The Host
/// therefore keeps its admission window open for the sum plus <see cref="HostAdmissionMarginSeconds"/>,
/// measured from its session bootstrap (never earlier than the session opening), so a member that
/// completes the connection inside its own budget is never cut off by the Host. Nothing forces the
/// Client to start joining before the session opens; the margin absorbs the release-to-open latency.
/// </remarks>
public static class RaidAdmissionWindows
{
    /// <summary>Default for the serialized client availability retry budget (from the Client's own launch).</summary>
    public const float ClientSessionAvailabilitySeconds = 40f;

    /// <summary>How long a connected Client waits for the Host to admit it.</summary>
    public const float ClientAdmissionSeconds = 30f;

    /// <summary>Slack covering scheduling and replication latency between Host and Clients.</summary>
    public const float HostAdmissionMarginSeconds = 5f;

    /// <summary>How long the Host waits for the complete frozen cohort to be admitted.</summary>
    public const float HostAdmissionSeconds =
        ClientSessionAvailabilitySeconds + ClientAdmissionSeconds + HostAdmissionMarginSeconds;

    /// <summary>How long a created runner waits for its initial Fusion scene callback; it precedes the Host window.</summary>
    public const float InitialSceneWaitSeconds = 30f;
}
