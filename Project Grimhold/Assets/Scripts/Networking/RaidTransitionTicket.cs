public readonly struct RaidTransitionTicket
{
    public RaidConnectionRequest Request { get; }
    public PlayerClassId SelectedBuild { get; }
    public SessionConnectionState State { get; }
    public PendingLoadoutReservation LoadoutReservation { get; }
    public RaidLaunchContext LaunchContext { get; }

    public bool HasLoadoutReservation => LoadoutReservation != null;
    public bool IsValid => Request.IsValid && PlayerJoinDataCodec.IsSupported(SelectedBuild) &&
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
        PlayerClassId selectedBuild,
        SessionConnectionState state)
        : this(request, null, selectedBuild, state, null)
    {
    }

    public RaidTransitionTicket(
        in RaidConnectionRequest request,
        PendingLoadoutReservation loadoutReservation,
        PlayerClassId selectedBuild,
        SessionConnectionState state,
        RaidLaunchContext launchContext)
    {
        Request = request;
        LoadoutReservation = loadoutReservation?.Clone();
        SelectedBuild = selectedBuild;
        State = state;
        LaunchContext = launchContext;
    }

    public RaidTransitionTicket WithState(SessionConnectionState state)
    {
        return new RaidTransitionTicket(Request, LoadoutReservation, SelectedBuild, state, LaunchContext);
    }
}
