using UnityEngine;

/// <summary>
/// Pure deterministic rules for reconnect budget consumption.
/// </summary>
public static class ReconnectBudgetRules
{
    /// <summary>
    /// Converts a configuration duration in seconds to simulation ticks.
    /// </summary>
    public static int CalculateInitialTicks(float durationSeconds, int tickRate)
    {
        if (durationSeconds <= 0f || tickRate <= 0)
        {
            return 0;
        }

        return Mathf.CeilToInt(durationSeconds * tickRate);
    }

    /// <summary>
    /// Consumes a budget tick if the participant is disconnected and still Raiding.
    /// The budget is sticky at zero and does not reset.
    /// </summary>
    public static int ConsumeTick(int remainingTicks, bool isDisconnected, RaidParticipantState state)
    {
        if (!isDisconnected || state != RaidParticipantState.Raiding || remainingTicks <= 0)
        {
            return remainingTicks;
        }

        return remainingTicks - 1;
    }
}
