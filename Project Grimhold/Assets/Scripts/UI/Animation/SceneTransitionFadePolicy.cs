/// <summary>Which local session states keep the screen covered while scenes and runners change.</summary>
public static class SceneTransitionFadePolicy
{
    public static bool ShouldCover(SessionConnectionState state)
    {
        switch (state)
        {
            case SessionConnectionState.ConnectingTown:
            case SessionConnectionState.ConnectingRaid:
            case SessionConnectionState.ReturningTown:
                return true;
            default:
                return false;
        }
    }
}
