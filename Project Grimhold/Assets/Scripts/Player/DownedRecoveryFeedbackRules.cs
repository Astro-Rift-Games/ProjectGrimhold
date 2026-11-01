/// <summary>
/// Pure presentation rules for the Downed and revive feedback (GD 13 section 14). Reads already
/// replicated values; owns no state and never feeds simulation.
/// </summary>
public static class DownedRecoveryFeedbackRules
{
    public const string CompletedMessage = "Reanimación completada";
    public const string SelfCompletedMessage = "Auto-reanimación completada";
    public const string HandoffReceivedMessage = "Auto-reanimador recibido";
    public const string HandoffGivenMessage = "Auto-reanimador entregado";
    public const string HandoffRejectedMessage = "Entrega fallida: el aliado no puede recibirlo";

    private const string DownedLabel = "ABATIDO";

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
        return InterruptionMessage(reason, RecoveryKind.Assisted);
    }

    /// <summary>Interruption message naming the recovery form that was interrupted.</summary>
    public static string InterruptionMessage(DownedRecoveryInterruptReason reason, RecoveryKind sessionKind)
    {
        if (reason == DownedRecoveryInterruptReason.HandoffRejected)
        {
            return HandoffRejectedMessage;
        }

        string cause = InterruptionCause(reason);
        return cause == null ? null : $"{FormLabel(sessionKind)} interrumpida: {cause}";
    }

    /// <summary>Message for an assisted outcome, or null when nothing should be shown.</summary>
    public static string MessageFor(RecoveryOutcomeKind kind, DownedRecoveryInterruptReason reason)
    {
        return MessageFor(kind, reason, RecoveryKind.Assisted, true);
    }

    /// <summary>
    /// Message for an outcome of <paramref name="sessionKind"/>, or null when nothing should be
    /// shown. <paramref name="asRecipient"/> is true for the Downed player, false for the teammate.
    /// </summary>
    public static string MessageFor(
        RecoveryOutcomeKind kind,
        DownedRecoveryInterruptReason reason,
        RecoveryKind sessionKind,
        bool asRecipient)
    {
        switch (kind)
        {
            case RecoveryOutcomeKind.Completed:
                return sessionKind switch
                {
                    RecoveryKind.Self => SelfCompletedMessage,
                    RecoveryKind.Handoff => asRecipient ? HandoffReceivedMessage : HandoffGivenMessage,
                    _ => CompletedMessage
                };
            case RecoveryOutcomeKind.Interrupted:
                return InterruptionMessage(reason, sessionKind);
            default:
                return null;
        }
    }

    /// <summary>
    /// Downed status line: the running recovery form, otherwise Self-reviver availability
    /// (GD 13 section 14: the Downed player must know whether a Self-revive is available).
    /// </summary>
    public static string DownedStateText(RecoveryKind activeSessionKind, bool carriesSelfReviver, bool selfReviveUsed)
    {
        switch (activeSessionKind)
        {
            case RecoveryKind.Assisted:
                return $"{DownedLabel} · Reanimando";
            case RecoveryKind.Self:
                return $"{DownedLabel} · Auto-reanimándote";
            case RecoveryKind.Handoff:
                return $"{DownedLabel} · Recibiendo Auto-reanimador";
        }

        if (!carriesSelfReviver)
        {
            return DownedLabel;
        }

        return selfReviveUsed
            ? $"{DownedLabel} · Auto-reanimador ya usado"
            : $"{DownedLabel} · pulsa [{InteractionPromptText.KeyLabel}] para auto-reanimarte";
    }

    /// <summary>Progress bar label for the running form, seen by the teammate or by the Downed player.</summary>
    public static string ProgressLabel(RecoveryKind sessionKind, bool asReviver)
    {
        switch (sessionKind)
        {
            case RecoveryKind.Self:
                return "Auto-reanimándote…";
            case RecoveryKind.Handoff:
                return asReviver ? "Entregando Auto-reanimador…" : "Recibiendo Auto-reanimador…";
            default:
                return asReviver ? "Reanimando…" : "Te están reanimando…";
        }
    }

    /// <summary>Teammate hint over a Downed ally; offers tap (handoff) and hold (revive) when both apply.</summary>
    public static string AllyDownedText(bool isInRange, bool canHandoff)
    {
        if (!isInRange)
        {
            return "Aliado abatido";
        }

        string key = InteractionPromptText.KeyLabel;
        return canHandoff
            ? $"Aliado abatido · toca [{key}]: entregar Auto-reanimador · mantén [{key}]: reanimar"
            : $"Aliado abatido · mantén [{key}] para reanimar";
    }

    private static string FormLabel(RecoveryKind sessionKind)
    {
        return sessionKind switch
        {
            RecoveryKind.Self => "Auto-reanimación",
            RecoveryKind.Handoff => "Entrega",
            _ => "Reanimación"
        };
    }

    private static string InterruptionCause(DownedRecoveryInterruptReason reason)
    {
        switch (reason)
        {
            case DownedRecoveryInterruptReason.InteractReleased:
                return "acción soltada";
            case DownedRecoveryInterruptReason.ReviverMoved:
            case DownedRecoveryInterruptReason.DownedMoved:
                return "movimiento";
            case DownedRecoveryInterruptReason.OutOfRange:
                return "fuera de rango";
            case DownedRecoveryInterruptReason.ReviverDamaged:
            case DownedRecoveryInterruptReason.DownedDamaged:
                return "daño recibido";
            case DownedRecoveryInterruptReason.IncompatibleReviverAction:
                return "otra acción";
            case DownedRecoveryInterruptReason.ReviverDowned:
                return "el reanimador cayó";
            case DownedRecoveryInterruptReason.ReviverDisconnected:
                return "reanimador desconectado";
            default:
                return null;
        }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
