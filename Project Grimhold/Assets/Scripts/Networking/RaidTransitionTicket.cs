public readonly struct RaidTransitionTicket
{
    public RaidConnectionRequest Request { get; }
    public SessionConnectionState State { get; }
    public PendingLoadoutReservation LoadoutReservation { get; }
    public RaidLaunchContext LaunchContext { get; }

    public bool HasLoadoutReservation => LoadoutReservation != null;
    public bool IsValid => Request.IsValid &&
                           LaunchContext != null && HasLoadoutReservation &&
                           LaunchContext.RaidCode.IsValid &&
                           string.Equals(Request.RaidId, LaunchContext.RaidCode.RaidId, System.StringComparison.Ordinal) &&
                           string.Equals(Request.SessionName, LaunchContext.RaidCode.SessionName, System.StringComparison.Ordinal) &&
                           LaunchContext.LocalProfileId.IsValid &&
                           Request.Role == (LaunchContext.LocalProfileId == LaunchContext.HostProfileId
                               ? RaidConnectionRole.Host
                               : RaidConnectionRole.Client);

    public RaidTransitionTicket(
        in RaidConnectionRequest request,
        SessionConnectionState state)
        : this(request, null, state, null)
    {
    }

    public RaidTransitionTicket(
        in RaidConnectionRequest request,
        PendingLoadoutReservation loadoutReservation,
        SessionConnectionState state,
        RaidLaunchContext launchContext)
    {
        Request = request;
        LoadoutReservation = loadoutReservation?.Clone();
        State = state;
        LaunchContext = launchContext;
    }

    public RaidTransitionTicket WithState(SessionConnectionState state)
    {
        return new RaidTransitionTicket(Request, LoadoutReservation, state, LaunchContext);
    }
}
