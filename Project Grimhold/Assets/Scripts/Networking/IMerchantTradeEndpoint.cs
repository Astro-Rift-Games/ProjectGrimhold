using System;
using System.Collections.Generic;

/// <summary>
/// Local receiver of State Authority answers for one open merchant session.
/// </summary>
public interface IMerchantTradeClient
{
    /// <summary>
    /// Handles a trade response. Returning false means the ticket was not and will not be
    /// persisted by this client; the endpoint then reports it as not persisted.
    /// </summary>
    bool HandleTradeResponse(MerchantTradeResponse response);

    /// <summary>
    /// A trade of this profile still reserving stock. The client reports its real outcome unless
    /// the request is still in use and may yet be persisted.
    /// </summary>
    void HandlePendingTrade(Guid requestId, ShopTransactionId transactionId);
}

/// <summary>
/// Local client view of one merchant: its offering, replicated available stock, trade submission
/// to State Authority, outcome reports and resolution of the profile's pending trades. Answers
/// reach the single registered <see cref="IMerchantTradeClient"/>; an approved response no client
/// accepts is reported as not persisted so its reservation is released.
/// </summary>
public interface IMerchantTradeEndpoint
{
    LootDefinitionCatalog Catalog { get; }
    IReadOnlyList<MerchantStockItem> Stock { get; }

    /// <summary>Unreserved units, <see cref="MerchantStockItem.UnlimitedQuantity"/>, or zero when not offered.</summary>
    int GetAvailableStock(LootId lootId);

    /// <summary>Raised when the replicated available stock changes.</summary>
    event Action StockChanged;

    /// <summary>Raised when the merchant's State Authority changes; in-flight messages may be lost.</summary>
    event Action AuthorityChanged;

    /// <summary>Sends one confirmation. Returns false when it could not be sent.</summary>
    bool TrySubmitTrade(
        ProfileId profileId,
        Guid requestId,
        IReadOnlyList<MerchantTradeLine> purchases,
        IReadOnlyList<MerchantTradeLine> sales);

    /// <summary>Reports the local persistence outcome of an approved ticket to State Authority.</summary>
    void ReportTradeOutcome(Guid requestId, ShopTransactionId transactionId, bool persisted);

    /// <summary>Asks State Authority for this profile's pending trades, delivered to the client.</summary>
    void RequestPendingTrades(ProfileId profileId);

    void SetTradeClient(IMerchantTradeClient client);

    /// <summary>Removes the client only when it is still the registered one.</summary>
    void ClearTradeClient(IMerchantTradeClient client);
}
