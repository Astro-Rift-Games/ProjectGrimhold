using System;

/// <summary>What one simulation step of an immobilize effect must do. Flags: a final tick and the end may coincide.</summary>
[Flags]
internal enum ImmobilizeStep
{
    None = 0,
    ApplyTick = 1,
    End = 2
}

/// <summary>Pure, deterministic rules of the immobilize + periodic damage effect. No Unity scene or Fusion state is read here.</summary>
internal static class ImmobilizeRules
{
    /// <summary>Periodic damage may be zero (immobilize only); the duration and the tick interval must be positive.</summary>
    public static bool TryValidateConfiguration(float durationSeconds, float damagePerTick, float tickIntervalSeconds,
        out string error)
    {
        if (!IsPositiveFinite(durationSeconds))
            error = "Immobilize duration seconds must be finite and greater than zero.";
        else if (!IsNonNegativeFinite(damagePerTick))
            error = "Immobilize damage per tick must be finite and not negative.";
        else if (!IsPositiveFinite(tickIntervalSeconds))
            error = "Immobilize tick interval seconds must be finite and greater than zero.";
        else
            error = string.Empty;
        return error.Length == 0;
    }

    /// <summary>
    /// The work of one simulation step. A dead target ends the effect without another tick. Otherwise a due tick is
    /// applied before the end is considered, so a tick that falls on the last instant of the duration still lands:
    /// damage lasts exactly as long as the immobilization, boundary included. At most one tick is decided per step.
    /// </summary>
    public static ImmobilizeStep Decide(bool isActive, bool targetAlive, bool durationExpired, bool tickDue)
    {
        if (!isActive) return ImmobilizeStep.None;
        if (!targetAlive) return ImmobilizeStep.End;

        ImmobilizeStep step = ImmobilizeStep.None;
        if (tickDue) step |= ImmobilizeStep.ApplyTick;
        if (durationExpired) step |= ImmobilizeStep.End;
        return step;
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
    private static bool IsNonNegativeFinite(float value) => value >= 0f && !float.IsInfinity(value);
}
