/// <summary>
/// Final outcome of one merchant trade confirmation, presented to the player.
/// </summary>
public enum MerchantTransactionResult
{
    /// <summary>The ticket was persisted: Gold and Inventory changed.</summary>
    Success,

    /// <summary>The ticket was already persisted before (idempotent retry); nothing changed twice.</summary>
    AlreadyApplied,

    /// <summary>State Authority rejected the request (stock, unknown item or conflicting request).</summary>
    RejectedByMerchant,

    /// <summary>The confirmed profile rejected the ticket (Gold, owned units or capacity).</summary>
    RejectedByProfile,

    /// <summary>The ticket could not be persisted; the profile is unchanged.</summary>
    PersistenceFailed,

    /// <summary>The confirmation could not be sent to State Authority.</summary>
    SubmissionFailed,

    /// <summary>No answer arrived in time or authority changed; the same request may be retried.</summary>
    NoResponse
}
