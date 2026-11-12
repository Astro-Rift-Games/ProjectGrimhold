/// <summary>
/// Pure outcome of the corpse-generation attempt that a voluntary Abandon requires (FIX-53 S7).
/// </summary>
public readonly struct SoloAbandonCorpseResolution
{
    private SoloAbandonCorpseResolution(
        bool proceedsToAborted,
        ExpeditionProgressionFinalizationCause finalizationCause,
        int failureSequence)
    {
        ProceedsToAborted = proceedsToAborted;
        FinalizationCause = finalizationCause;
        FailureSequence = failureSequence;
    }

    /// <summary>True when the equipment now lives in the corpse and the participant may become Aborted.</summary>
    public bool ProceedsToAborted { get; }

    /// <summary>Finalization cause the participant must hold after the attempt.</summary>
    public ExpeditionProgressionFinalizationCause FinalizationCause { get; }

    /// <summary>Replicated failure counter the requesting player observes to report the failure.</summary>
    public int FailureSequence { get; }

    public static SoloAbandonCorpseResolution Resolve(
        bool corpseConverted,
        ExpeditionProgressionFinalizationCause pendingCause,
        int failureSequence)
    {
        if (corpseConverted)
        {
            return new SoloAbandonCorpseResolution(true, pendingCause, failureSequence);
        }

        // The pending abandon is dropped so the next Abandon press starts a fresh attempt, and the
        // counter moves so the requesting player is told. unchecked: the counter may wrap.
        return new SoloAbandonCorpseResolution(
            false,
            ExpeditionProgressionFinalizationCause.None,
            unchecked(failureSequence + 1));
    }
}
