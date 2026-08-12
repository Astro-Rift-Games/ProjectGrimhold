/// <summary>
/// Represents the final logical outcome of a merchant transaction, intended to be
/// consumed by the UI to present the result to the player.
/// </summary>
public enum MerchantTransactionResult
{
    /// <summary>
    /// Transaction completed successfully (funds deducted, item secured, or vice versa).
    /// </summary>
    Success,

    /// <summary>
    /// The request was invalid locally (e.g. amount <= 0, item not in local catalog)
    /// and was not sent to the server.
    /// </summary>
    InvalidRequest,

    /// <summary>
    /// The master client rejected the transaction (e.g. protocol mismatch, unavailable item).
    /// </summary>
    RejectedByMerchant,

    /// <summary>
    /// The local persistent store rejected the purchase due to insufficient currency.
    /// </summary>
    InsufficientFunds,

    /// <summary>
    /// The local persistent store rejected the sale due to missing inventory items.
    /// </summary>
    MissingItems,

    /// <summary>
    /// The transaction was already applied (caught by idempotency protection).
    /// </summary>
    AlreadyApplied,

    /// <summary>
    /// The network request timed out (reserved for future implementation).
    /// </summary>
    Timeout
}
