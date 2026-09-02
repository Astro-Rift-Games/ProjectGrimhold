using System;

/// <summary>
/// Owns the deterministic local transition rules for the Town and raid connection lifecycle.
/// It contains no Unity or Fusion dependencies and never starts or stops network sessions.
/// </summary>
public sealed class SessionConnectionStateMachine
{
    public SessionConnectionState State { get; private set; }

    public SessionConnectionStateMachine(
        SessionConnectionState initialState = SessionConnectionState.MainMenu)
    {
        State = initialState;
    }

    public bool TryTransition(SessionConnectionState nextState)
    {
        if (!CanTransition(State, nextState))
        {
            return false;
        }

        State = nextState;
        return true;
    }

    public static bool CanTransition(
        SessionConnectionState currentState,
        SessionConnectionState nextState)
    {
        if (currentState == nextState)
        {
            return false;
        }

        return currentState switch
        {
            SessionConnectionState.MainMenu =>
                nextState == SessionConnectionState.ConnectingTown ||
                nextState == SessionConnectionState.ConnectingRaid ||
                nextState == SessionConnectionState.Failed,
            SessionConnectionState.ConnectingTown =>
                nextState == SessionConnectionState.Town ||
                nextState == SessionConnectionState.Failed,
            SessionConnectionState.Town =>
                nextState == SessionConnectionState.PreparingRaid ||
                nextState == SessionConnectionState.ReturningTown ||
                nextState == SessionConnectionState.MainMenu ||
                nextState == SessionConnectionState.Failed,
            SessionConnectionState.PreparingRaid =>
                nextState == SessionConnectionState.ConnectingRaid ||
                nextState == SessionConnectionState.ReturningTown ||
                nextState == SessionConnectionState.Failed,
            SessionConnectionState.ConnectingRaid =>
                nextState == SessionConnectionState.Raid ||
                nextState == SessionConnectionState.ReturningTown ||
                nextState == SessionConnectionState.Failed,
            SessionConnectionState.Raid =>
                nextState == SessionConnectionState.ReturningTown ||
                nextState == SessionConnectionState.Failed,
            SessionConnectionState.ReturningTown =>
                nextState == SessionConnectionState.Town ||
                nextState == SessionConnectionState.Failed,
            SessionConnectionState.Failed =>
                nextState == SessionConnectionState.ConnectingTown ||
                nextState == SessionConnectionState.ConnectingRaid ||
                nextState == SessionConnectionState.ReturningTown ||
                nextState == SessionConnectionState.MainMenu,
            _ => throw new ArgumentOutOfRangeException(nameof(currentState), currentState, null)
        };
    }
}
