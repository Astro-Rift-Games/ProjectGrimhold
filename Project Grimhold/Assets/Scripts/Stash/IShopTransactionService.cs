/// <summary>
/// Persistence boundary for Commerce. Applies one authority-issued trade ticket to one profile as a
/// single atomic transaction, idempotent by the ticket's transaction id.
/// </summary>
public interface IShopTransactionService
{
    /// <summary>
    /// Applies every purchase and sale line of the ticket, or none of them.
    /// <see cref="StashOperationResult.AlreadyApplied"/> means the ticket was persisted before.
    /// </summary>
    StashOperationResult TryExecuteTrade(ProfileId profileId, MerchantTradeTicket ticket);

    /// <summary>
    /// Whether the ticket with this transaction id is already persisted in the confirmed profile.
    /// Used to resolve reservations whose outcome report never reached State Authority.
    /// </summary>
    bool IsTradeApplied(ProfileId profileId, ShopTransactionId transactionId);
}
