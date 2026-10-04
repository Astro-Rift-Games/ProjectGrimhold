/// <summary>
/// Pure deterministic rules for Downed recovery sessions (GD 13 sections 6 and 18).
/// Holds no state; the session owner feeds it authoritative values each tick.
/// </summary>
internal static class DownedRecoveryRules
{
    /// <summary>True when the reviver may open an assisted session on the target.</summary>
    internal static bool CanStartAssisted(
        bool reviverAlive,
        bool reviverDowned,
        bool sameTeam,
        float distance,
        float range,
        bool targetDowned,
        bool targetHasValidSession,
        bool reviverAlreadyReviving)
    {
        return reviverAlive &&
               !reviverDowned &&
               sameTeam &&
               targetDowned &&
               !targetHasValidSession &&
               !reviverAlreadyReviving &&
               IsRangeConfigured(range) &&
               IsWithinRange(distance, range);
    }

    /// <summary>Returns the first matching interruption reason, or None when the session stands.</summary>
    internal static DownedRecoveryInterruptReason EvaluateInterruption(
        in DownedRecoveryInterruptionSnapshot snapshot)
    {
        if (!snapshot.DownedIsDowned || snapshot.SessionCycle != snapshot.DownedCycle)
        {
            return DownedRecoveryInterruptReason.DownedDefeated;
        }

        if (!snapshot.ReviverConnected)
        {
            return DownedRecoveryInterruptReason.ReviverDisconnected;
        }

        if (snapshot.ReviverDowned)
        {
            return DownedRecoveryInterruptReason.ReviverDowned;
        }

        if (snapshot.CurrentReviverHealth < snapshot.PreviousReviverHealth)
        {
            return DownedRecoveryInterruptReason.ReviverDamaged;
        }

        if (snapshot.DownedDamaged)
        {
            return DownedRecoveryInterruptReason.DownedDamaged;
        }

        if (snapshot.IncompatibleReviverAction)
        {
            return DownedRecoveryInterruptReason.IncompatibleReviverAction;
        }

        if (!IsWithinRange(snapshot.Distance, snapshot.Range))
        {
            return DownedRecoveryInterruptReason.OutOfRange;
        }

        if (snapshot.ReviverMoving)
        {
            return DownedRecoveryInterruptReason.ReviverMoved;
        }

        if (snapshot.DownedMoving)
        {
            return DownedRecoveryInterruptReason.DownedMoved;
        }

        if (!snapshot.ReviverInteractHeld)
        {
            return DownedRecoveryInterruptReason.InteractReleased;
        }

        return DownedRecoveryInterruptReason.None;
    }

    /// <summary>A session is valid only for its own Downed cycle while the target is still Downed.</summary>
    internal static bool IsSessionValid(RecoveryKind kind, int sessionCycle, int downedCycle, bool isDowned)
    {
        return kind != RecoveryKind.None &&
               isDowned &&
               sessionCycle > 0 &&
               sessionCycle == downedCycle;
    }

    private static bool IsRangeConfigured(float range)
    {
        return !float.IsNaN(range) && !float.IsInfinity(range) && range > 0f;
    }

    private static bool IsWithinRange(float distance, float range)
    {
        return !float.IsNaN(distance) && !float.IsInfinity(distance) && distance >= 0f && distance <= range;
    }
}
