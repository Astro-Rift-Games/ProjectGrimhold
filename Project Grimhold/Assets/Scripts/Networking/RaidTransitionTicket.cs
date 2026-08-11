public readonly struct RaidTransitionTicket
{
    public RaidConnectionRequest Request { get; }
    public RaidLaunchManifest Manifest { get; }
    public PlayerClassId SelectedBuild { get; }
    public SessionConnectionState State { get; }
    public PendingLoadoutReservation LoadoutReservation { get; }
    public RaidLaunchContext LaunchContext { get; }

    public bool HasManifest => Manifest.IsValid;
    public bool HasLoadoutReservation => LoadoutReservation != null;
    public bool IsValid => Request.IsValid && PlayerJoinDataCodec.IsSupported(SelectedBuild) &&
                           (!HasManifest || (HasLoadoutReservation &&
                            string.Equals(Request.RaidId, Manifest.RaidId, System.StringComparison.Ordinal) &&
                            string.Equals(Request.SessionName, Manifest.SessionName, System.StringComparison.Ordinal)));

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
        PendingLoadoutReservation loadoutReservation,
        PlayerClassId selectedBuild,
        SessionConnectionState state)
        : this(request, manifest, loadoutReservation, selectedBuild, state, null)
    {
    }

    public RaidTransitionTicket(
        in RaidConnectionRequest request,
        in RaidLaunchManifest manifest,
        PendingLoadoutReservation loadoutReservation,
        PlayerClassId selectedBuild,
        SessionConnectionState state,
        RaidLaunchContext launchContext)
    {
        Request = request;
        Manifest = manifest;
        LoadoutReservation = loadoutReservation?.Clone();
        SelectedBuild = selectedBuild;
        State = state;
        LaunchContext = launchContext ?? CreateContext(request, manifest);
    }

    public RaidTransitionTicket(
        in RaidConnectionRequest request,
        in RaidLaunchManifest manifest,
        PlayerClassId selectedBuild,
        SessionConnectionState state)
        : this(request, manifest, null, selectedBuild, state)
    {
    }

    public RaidTransitionTicket WithState(SessionConnectionState state)
    {
        return new RaidTransitionTicket(Request, Manifest, LoadoutReservation, SelectedBuild, state, LaunchContext);
    }

    private static RaidLaunchContext CreateContext(
        in RaidConnectionRequest request,
        in RaidLaunchManifest manifest)
    {
        return new RaidLaunchContext(
            manifest.RaidCode.IsValid ? manifest.RaidCode : request.RaidCode,
            manifest.HostProfileId,
            manifest.AdmittedProfiles,
            default);
    }
}
