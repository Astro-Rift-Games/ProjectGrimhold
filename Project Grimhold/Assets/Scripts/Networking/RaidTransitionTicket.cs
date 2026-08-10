public readonly struct RaidTransitionTicket
{
    public RaidConnectionRequest Request { get; }
    public PlayerClassId SelectedBuild { get; }
    public SessionConnectionState State { get; }

    public bool IsValid => Request.IsValid && PlayerJoinDataCodec.IsSupported(SelectedBuild);

    public RaidTransitionTicket(
        in RaidConnectionRequest request,
        PlayerClassId selectedBuild,
        SessionConnectionState state)
    {
        Request = request;
        SelectedBuild = selectedBuild;
        State = state;
    }

    public RaidTransitionTicket WithState(SessionConnectionState state)
    {
        return new RaidTransitionTicket(Request, SelectedBuild, state);
    }
}
