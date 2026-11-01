/// <summary>
/// Pure deterministic rules for Downed recovery sessions (GD 13 sections 6, 7, 7.1, 8 and 18).
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

    /// <summary>
    /// True when a Downed player may start a Self-revive: carries a Self-reviver, has no session
    /// running and has not used the one successful Self-revive allowed per expedition.
    /// </summary>
    internal static bool CanStartSelf(bool isDowned, bool hasValidSession, bool carriesSelfReviver, bool selfReviveUsed)
    {
        return isDowned && !hasValidSession && carriesSelfReviver && !selfReviveUsed;
    }

    /// <summary>
    /// True when an Active teammate may hand their Self-reviver to the Downed target: the assisted
    /// start conditions, plus an item to give and a target that can receive and still use it.
    /// </summary>
    internal static bool CanStartHandoff(
        bool giverAlive,
        bool giverDowned,
        bool sameTeam,
        float distance,
        float range,
        bool targetDowned,
        bool targetHasValidSession,
        bool giverAlreadyReviving,
        bool giverCarriesSelfReviver,
        bool targetCanReceive,
        bool targetSelfReviveUsed)
    {
        return giverCarriesSelfReviver &&
               targetCanReceive &&
               !targetSelfReviveUsed &&
               CanStartAssisted(giverAlive, giverDowned, sameTeam, distance, range,
                   targetDowned, targetHasValidSession, giverAlreadyReviving);
    }

    /// <summary>
    /// Tap-versus-hold resolution: an assisted session released within the tap window becomes a
    /// handoff when one is possible. Any other interruption, or a longer hold, keeps its outcome.
    /// </summary>
    internal static bool ShouldConvertToHandoff(
        DownedRecoveryInterruptReason interruption,
        float elapsedSeconds,
        float tapWindowSeconds,
        bool canHandoff)
    {
        return canHandoff &&
               interruption == DownedRecoveryInterruptReason.InteractReleased &&
               !float.IsNaN(elapsedSeconds) && elapsedSeconds <= tapWindowSeconds;
    }

    /// <summary>
    /// Self-revive interruptions. Voluntary movement is locked, so movement here is a displacement
    /// that still moved the avatar; damage arrives through the damage observer and disconnect never
    /// interrupts (GD 13 sections 7, 11 and 18).
    /// </summary>
    internal static DownedRecoveryInterruptReason EvaluateSelfInterruption(
        bool downedIsDowned,
        int sessionCycle,
        int downedCycle,
        bool downedMoving)
    {
        if (!downedIsDowned || sessionCycle != downedCycle)
        {
            return DownedRecoveryInterruptReason.DownedDefeated;
        }

        return downedMoving ? DownedRecoveryInterruptReason.DownedMoved : DownedRecoveryInterruptReason.None;
    }

    /// <summary>
    /// Handoff interruptions: the giver's assisted conditions (damage, movement, range, incompatible
    /// action, Downed, disconnect) without holding Interact, and without the Downed's own movement,
    /// which only matters when it breaks the range (GD 13 sections 7.1 and 8).
    /// </summary>
    internal static DownedRecoveryInterruptReason EvaluateHandoffInterruption(
        in DownedRecoveryInterruptionSnapshot snapshot)
    {
        DownedRecoveryInterruptReason reason = EvaluateInterruption(snapshot);
        return reason == DownedRecoveryInterruptReason.InteractReleased ||
               reason == DownedRecoveryInterruptReason.DownedMoved
            ? DownedRecoveryInterruptReason.None
            : reason;
    }

    /// <summary>Only a revive pauses the drain; the handoff does not (GD 13 section 4).</summary>
    internal static bool PausesDrain(RecoveryKind kind)
    {
        return kind == RecoveryKind.Assisted || kind == RecoveryKind.Self;
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
