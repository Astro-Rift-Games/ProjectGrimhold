/// <summary>High-level outcome of a Town ability preparation attempt.</summary>
public enum TownAbilityMutationOutcome
{
    /// <summary>The change was persisted.</summary>
    Success,

    /// <summary>Town does not currently allow ability changes (player Ready); nothing was attempted.</summary>
    BlockedByReadyState,

    /// <summary>The preparation rules rejected the change; see the underlying preparation result.</summary>
    Rejected
}
