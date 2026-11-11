/// <summary>What the Raid Host does once its own participation is resolved (FIX-50).</summary>
public enum HostHandoffDecision
{
    /// <summary>No handoff: the Host keeps the role (client, no active Raid, or exit unresolved).</summary>
    Stay,
    /// <summary>Pass the Host role to a connected Raiding participant through Host Migration.</summary>
    HandOff,
    /// <summary>Nobody active remains: close the Raid normally after pending commits.</summary>
    CloseNormally
}

/// <summary>
/// Pure rule for a voluntary Host exit. The Host role only moves inside an active Dungeon Raid,
/// after the outgoing Host resolved its own participation, and only to a connected Raiding peer.
/// </summary>
public static class HostHandoffPolicy
{
    /// <summary>
    /// Whether a participant exit request (Abandon or Return) is admissible for its requester role.
    /// The operational Host may resolve its own participation only during an active Raid, because
    /// that resolution precedes a handoff; afterwards it keeps the Results return path.
    /// </summary>
    public static bool AcceptsExitRequest(bool requesterIsHost, bool isRaidInProgress)
    {
        return !requesterIsHost || isRaidInProgress;
    }

    /// <summary>
    /// Whether the outgoing Host already closed its own participation with a persisted result:
    /// an authorized Return (after Defeat Results or a completed extraction), or a confirmed
    /// Abandon whose corpse and progression result exist.
    /// </summary>
    public static bool IsHostExitResolved(
        RaidParticipantState state,
        ExpeditionProgressionFinalizationCause finalizationCause,
        bool isReturnAuthorized,
        bool hasProgressionResultSnapshot)
    {
        if (isReturnAuthorized)
        {
            return true;
        }

        return state == RaidParticipantState.Aborted &&
               finalizationCause ==
                   ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed &&
               hasProgressionResultSnapshot;
    }

    /// <summary>
    /// Whether a completed Abandon also authorizes Return. Only the operational Host does, and only
    /// once its progression result is durably committed, so the handoff never precedes the save:
    /// IsReturnAuthorized is the replicated proof the replacement Host can observe, because the
    /// progression result itself is local to the outgoing Host. Clients keep their own Return request.
    /// </summary>
    public static bool AuthorizesReturnOnResolvedAbandon(
        bool requestedByOperationalHost,
        bool isDurablyCommitted)
    {
        return requestedByOperationalHost && isDurablyCommitted;
    }

    /// <summary>
    /// Whether the Host's Results Return must travel through the participant Return RPC (so the
    /// Host resolves like a client and hands off) instead of the Finished-match global return.
    /// </summary>
    public static bool ShouldRouteHostReturnThroughParticipant(
        bool isMatchInProgress,
        int connectedRaidingRemoteParticipants)
    {
        return isMatchInProgress && connectedRaidingRemoteParticipants > 0;
    }

    /// <summary>
    /// Whether the coordinator may start a handoff attempt now: one attempt at a time, only while
    /// idle in a Raid, and not before the retry delay of a previous non-handoff outcome elapsed.
    /// </summary>
    public static bool ShouldAttemptHandoff(
        bool attemptInFlight,
        bool operationActive,
        bool coordinatorIsInRaid,
        float now,
        float notBefore)
    {
        return !attemptInFlight && !operationActive && coordinatorIsInRaid && now >= notBefore;
    }

    public static HostHandoffDecision Decide(
        bool isServer,
        bool isRaidInProgress,
        bool isHostExitResolved,
        int connectedRaidingRemoteParticipants)
    {
        if (!isServer || !isRaidInProgress || !isHostExitResolved)
        {
            return HostHandoffDecision.Stay;
        }

        return connectedRaidingRemoteParticipants > 0
            ? HostHandoffDecision.HandOff
            : HostHandoffDecision.CloseNormally;
    }
}
