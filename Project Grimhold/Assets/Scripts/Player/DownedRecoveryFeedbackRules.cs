/// <summary>
/// Pure presentation rules for the Downed and revive feedback (GD 13 section 14). Reads already
/// replicated values; owns no state and never feeds simulation.
/// </summary>
public static class DownedRecoveryFeedbackRules
{
    public const string CompletedMessage = "Reanimación completada";

    /// <summary>Normalized revive progress in [0, 1]; invalid inputs read as no progress.</summary>
    public static float ProgressFraction(float remainingSeconds, float durationSeconds)
    {
        if (!IsFinite(remainingSeconds) || !IsFinite(durationSeconds) || durationSeconds <= 0f)
        {
            return 0f;
        }

        float fraction = 1f - remainingSeconds / durationSeconds;
        return fraction < 0f ? 0f : fraction > 1f ? 1f : fraction;
    }

    /// <summary>Normalized Downed reserve in [0, 1]; invalid inputs read as empty.</summary>
    public static float ReserveFraction(float downedHealth, float initialDownedHealth)
    {
        if (!IsFinite(downedHealth) || !IsFinite(initialDownedHealth) || initialDownedHealth <= 0f)
        {
            return 0f;
        }

        float fraction = downedHealth / initialDownedHealth;
        return fraction < 0f ? 0f : fraction > 1f ? 1f : fraction;
    }

    /// <summary>
    /// Short Spanish message for an interruption, or null when none is shown. Definitive Defeat of
    /// the Downed has its own overlay, so it never produces an interruption message.
    /// </summary>
    public static string InterruptionMessage(DownedRecoveryInterruptReason reason)
    {
        switch (reason)
        {
            case DownedRecoveryInterruptReason.InteractReleased:
                return "Reanimación interrumpida: acción soltada";
            case DownedRecoveryInterruptReason.ReviverMoved:
            case DownedRecoveryInterruptReason.DownedMoved:
                return "Reanimación interrumpida: movimiento";
            case DownedRecoveryInterruptReason.OutOfRange:
                return "Reanimación interrumpida: fuera de rango";
            case DownedRecoveryInterruptReason.ReviverDamaged:
            case DownedRecoveryInterruptReason.DownedDamaged:
                return "Reanimación interrumpida: daño recibido";
            case DownedRecoveryInterruptReason.IncompatibleReviverAction:
                return "Reanimación interrumpida: otra acción";
            case DownedRecoveryInterruptReason.ReviverDowned:
                return "Reanimación interrumpida: el reanimador cayó";
            case DownedRecoveryInterruptReason.ReviverDisconnected:
                return "Reanimación interrumpida: reanimador desconectado";
            default:
                return null;
        }
    }

    /// <summary>Message for an outcome, or null when nothing should be shown.</summary>
    public static string MessageFor(RecoveryOutcomeKind kind, DownedRecoveryInterruptReason reason)
    {
        switch (kind)
        {
            case RecoveryOutcomeKind.Completed:
                return CompletedMessage;
            case RecoveryOutcomeKind.Interrupted:
                return InterruptionMessage(reason);
            default:
                return null;
        }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
