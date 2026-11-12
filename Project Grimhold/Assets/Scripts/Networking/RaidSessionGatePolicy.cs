/// <summary>
/// Why a session gate decision could not be applied yet.
/// </summary>
public enum RaidSessionGateDeferral
{
    None,
    NoStateAuthority,
    PhaseNotInProgress
}

/// <summary>
/// Outcome of a session gate evaluation.
/// </summary>
public readonly struct RaidSessionGateDecision
{
    public RaidSessionGateDecision(bool isApplicable, bool targetIsOpen, RaidSessionGateDeferral deferral)
    {
        IsApplicable = isApplicable;
        TargetIsOpen = targetIsOpen;
        Deferral = deferral;
    }

    /// <summary>True when the gate may be written now.</summary>
    public bool IsApplicable { get; }

    /// <summary>The IsOpen value the gate wants (open while a retained participant may reconnect).</summary>
    public bool TargetIsOpen { get; }

    /// <summary>Set when the decision must be re-applied later.</summary>
    public RaidSessionGateDeferral Deferral { get; }
}

/// <summary>
/// Pure decision for the in-progress Raid session gate: open while a retained participant can
/// still reconnect, closed otherwise. Not applicable until the controller is ready.
/// </summary>
public static class RaidSessionGatePolicy
{
    public static RaidSessionGateDecision Evaluate(
        bool hasRetainedParticipant,
        bool hasStateAuthority,
        NetworkMatchController.MatchPhase phase)
    {
        if (!hasStateAuthority)
        {
            return new RaidSessionGateDecision(
                false, hasRetainedParticipant, RaidSessionGateDeferral.NoStateAuthority);
        }

        if (phase != NetworkMatchController.MatchPhase.InProgress)
        {
            return new RaidSessionGateDecision(
                false, hasRetainedParticipant, RaidSessionGateDeferral.PhaseNotInProgress);
        }

        return new RaidSessionGateDecision(true, hasRetainedParticipant, RaidSessionGateDeferral.None);
    }
}

/// <summary>
/// Remembers a deferred gate decision so it is re-applied once the controller is ready, and
/// de-duplicates decision logging so only changes are reported.
/// </summary>
public sealed class RaidSessionGateTracker
{
    private string _lastLoggedSignature;

    public bool HasPendingDecision { get; private set; }

    /// <summary>The last IsOpen value this gate requested, or null before the first write.</summary>
    public bool? LastRequestedIsOpen { get; private set; }

    /// <summary>
    /// Decides from the last value this gate requested, never from the observed room state: the
    /// observed IsOpen can lag behind a write made moments earlier, which would swallow the change.
    /// </summary>
    public bool ShouldWrite(bool targetIsOpen)
    {
        return LastRequestedIsOpen != targetIsOpen;
    }

    public void MarkRequested(bool targetIsOpen)
    {
        LastRequestedIsOpen = targetIsOpen;
    }

    public bool ShouldLog(string signature)
    {
        if (signature == _lastLoggedSignature)
        {
            return false;
        }

        _lastLoggedSignature = signature;
        return true;
    }

    public void Record(RaidSessionGateDecision decision)
    {
        HasPendingDecision = !decision.IsApplicable;
    }

    public void MarkApplied()
    {
        HasPendingDecision = false;
    }
}
