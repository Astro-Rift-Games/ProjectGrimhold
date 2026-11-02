/// <summary>
/// Pure causal exposure rules for knockback into environmental hazards (Doc 18 §6.5, Doc 19 §8.1-§8.3).
/// A push links the first hazard the target enters while displaced; the link lasts while that hazard keeps
/// reporting presence and breaks on exit or when a later push moves the target into another hazard.
/// Responsibility is recorded for any pusher; ally/enemy eligibility for credit is resolved elsewhere.
/// </summary>
public static class HazardExposureCausalityRules
{
    /// <summary>Hazard and target simulation order within a tick is not guaranteed; tolerate one tick.</summary>
    public const int PresenceGraceTicks = 1;

    /// <summary>
    /// Records a Started or Replaced knockback. A non-player push (0) discards the pending cause because
    /// it replaced the previous displacement. A hazard the target already occupies is not a push toward it.
    /// </summary>
    public static void RecordAcceptedKnockback(ref HazardExposureCausalityState state,
        int pusherEntityId, int knockbackSequence, int tick)
    {
        if (pusherEntityId == 0)
        {
            ClearPending(ref state);
            return;
        }

        state.PendingPusherEntityId = pusherEntityId;
        state.PendingKnockbackSequence = knockbackSequence;
        state.PendingActiveTick = tick;
        state.PendingExcludedHazardId = IsRecent(state.LastPresenceTick, tick) ? state.LastPresenceHazardId : 0;
    }

    /// <summary>Expires a finished push and breaks a link whose hazard stopped reporting presence.</summary>
    public static void Tick(ref HazardExposureCausalityState state, bool knockbackActive, int tick)
    {
        if (state.PendingPusherEntityId != 0)
        {
            if (knockbackActive)
            {
                state.PendingActiveTick = tick;
            }
            else if (!IsRecent(state.PendingActiveTick, tick))
            {
                ClearPending(ref state);
            }
        }

        if (state.LinkedHazardId != 0 && !IsRecent(state.LinkedPresenceTick, tick))
        {
            BreakLink(ref state, HazardCausalityBreakReason.ExitedHazard, tick);
        }
    }

    /// <summary>A hazard reports that the target is inside its exposure area on this tick.</summary>
    public static void ReportPresence(ref HazardExposureCausalityState state, int hazardId, int tick)
    {
        if (hazardId == 0)
        {
            return;
        }

        state.LastPresenceHazardId = hazardId;
        state.LastPresenceTick = tick;
        if (state.LinkedHazardId == hazardId)
        {
            state.LinkedPresenceTick = tick;
            return;
        }

        // Voluntary entry, or a hazard occupied before the push: no causal link.
        if (state.PendingPusherEntityId == 0 || !IsRecent(state.PendingActiveTick, tick) ||
            state.PendingExcludedHazardId == hazardId)
        {
            return;
        }

        if (state.LinkedHazardId != 0)
        {
            // One link per target: the push that created it keeps its first hazard.
            if (state.LinkedKnockbackSequence == state.PendingKnockbackSequence)
            {
                return;
            }

            // A later push into a different hazard determinantly changes the previous exposure.
            BreakLink(ref state, HazardCausalityBreakReason.IndependentIntervention, tick);
        }

        state.LinkedHazardId = hazardId;
        state.LinkedPusherEntityId = state.PendingPusherEntityId;
        state.LinkedKnockbackSequence = state.PendingKnockbackSequence;
        state.LinkedPresenceTick = tick;
    }

    /// <summary>Resolves the indirect responsible for damage of <paramref name="hazardId"/> on this tick.</summary>
    public static bool TryResolveCauser(in HazardExposureCausalityState state, int hazardId, int tick,
        out int pusherEntityId)
    {
        bool linked = hazardId != 0 && state.LinkedHazardId == hazardId && IsRecent(state.LinkedPresenceTick, tick);
        pusherEntityId = linked ? state.LinkedPusherEntityId : 0;
        return linked;
    }

    /// <summary>Discards every pending and linked cause, keeping the break audit.</summary>
    public static void Reset(ref HazardExposureCausalityState state, HazardCausalityBreakReason reason, int tick)
    {
        if (state.LinkedHazardId != 0)
        {
            BreakLink(ref state, reason, tick);
        }

        ClearPending(ref state);
        state.LastPresenceHazardId = 0;
        state.LastPresenceTick = 0;
    }

    /// <summary>True when any cause or presence is recorded; lets callers skip redundant resets.</summary>
    public static bool HasCausalState(in HazardExposureCausalityState state) =>
        state.PendingPusherEntityId != 0 || state.LinkedHazardId != 0 || state.LastPresenceHazardId != 0;

    private static void BreakLink(ref HazardExposureCausalityState state, HazardCausalityBreakReason reason, int tick)
    {
        state.LastBreakReason = reason;
        state.LastBrokenHazardId = state.LinkedHazardId;
        state.LastBrokenPusherEntityId = state.LinkedPusherEntityId;
        state.LastBrokenKnockbackSequence = state.LinkedKnockbackSequence;
        state.LastBreakTick = tick;
        state.LinkedHazardId = 0;
        state.LinkedPusherEntityId = 0;
        state.LinkedKnockbackSequence = 0;
        state.LinkedPresenceTick = 0;
    }

    private static void ClearPending(ref HazardExposureCausalityState state)
    {
        state.PendingPusherEntityId = 0;
        state.PendingKnockbackSequence = 0;
        state.PendingActiveTick = 0;
        state.PendingExcludedHazardId = 0;
    }

    private static bool IsRecent(int observedTick, int tick)
    {
        int elapsed = tick - observedTick;
        return observedTick > 0 && elapsed >= 0 && elapsed <= PresenceGraceTicks;
    }
}
