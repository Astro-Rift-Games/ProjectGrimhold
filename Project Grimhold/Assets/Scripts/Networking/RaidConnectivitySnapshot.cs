/// <summary>
/// Immutable snapshot representing the connectivity state of a participant.
/// </summary>
public readonly struct RaidConnectivitySnapshot
{
    public readonly bool IsDisconnected;
    public readonly int RemainingBudgetTicks;
    public readonly RaidParticipantState State;

    public RaidConnectivitySnapshot(
        bool isDisconnected,
        int remainingBudgetTicks,
        RaidParticipantState state)
    {
        IsDisconnected = isDisconnected;
        RemainingBudgetTicks = remainingBudgetTicks;
        State = state;
    }

    /// <summary>
    /// True if the reconnect budget has been fully consumed.
    /// </summary>
    public bool IsBudgetExhausted => RemainingBudgetTicks <= 0;

    /// <summary>
    /// True if a teammate can make a decision regarding this disconnected participant.
    /// </summary>
    public bool IsDecisionAvailable => IsDisconnected && IsBudgetExhausted && State == RaidParticipantState.Raiding;

    /// <summary>
    /// True if the participant counts as an active or recoverable member for team-wide actions like extraction.
    /// Excludes Defeated, Aborted, and Extracted states.
    /// </summary>
    public bool IsActiveOrRecoverableMember => State == RaidParticipantState.Raiding;
}
