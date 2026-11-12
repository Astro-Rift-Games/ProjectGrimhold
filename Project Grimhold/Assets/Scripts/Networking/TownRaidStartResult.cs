/// <summary>Outcome of a Host's Start request, reported back to the requester by the State Authority.</summary>
public enum TownRaidStartResult
{
    /// <summary>The preparation froze its cohort and entered Launching.</summary>
    Started,

    /// <summary>A member's Town party (or the party directory) is not available yet, so teams cannot be derived.</summary>
    PartyUnavailable,

    /// <summary>The request violates the Start preconditions (not Host, not everyone Ready, wrong state).</summary>
    Rejected
}
