public readonly struct RaidTransitionTicket
{
    public RaidConnectionRequest Request { get; }
    public RaidLaunchManifest Manifest { get; }
    public PlayerClassId SelectedBuild { get; }
    public SessionConnectionState State { get; }

    public bool HasManifest => Manifest.IsValid;
    public bool IsValid => Request.IsValid && PlayerJoinDataCodec.IsSupported(SelectedBuild) &&
                           (!HasManifest ||
                            string.Equals(Request.RaidId, Manifest.RaidId, System.StringComparison.Ordinal) &&
                            string.Equals(Request.SessionName, Manifest.SessionName, System.StringComparison.Ordinal));

    public RaidTransitionTicket(
        in RaidConnectionRequest request,
        PlayerClassId selectedBuild,
        SessionConnectionState state)
        : this(request, default, selectedBuild, state)
    {
    }

    public RaidTransitionTicket(
        in RaidConnectionRequest request,
        in RaidLaunchManifest manifest,
        PlayerClassId selectedBuild,
        SessionConnectionState state)
    {
        Request = request;
        Manifest = manifest;
        SelectedBuild = selectedBuild;
        State = state;
    }

    public RaidTransitionTicket WithState(SessionConnectionState state)
    {
        return new RaidTransitionTicket(Request, Manifest, SelectedBuild, state);
    }
}
