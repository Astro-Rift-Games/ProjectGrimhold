/// <summary>
/// Why a merchant trade preview does not allow confirmation.
/// </summary>
public enum MerchantTradeBlockReason
{
    /// <summary>The draft has no purchase or sale lines.</summary>
    EmptyDraft,

    /// <summary>The draft is already submitted and awaiting its outcome.</summary>
    SubmissionInFlight,

    /// <summary>The line's loot is not defined in the economy configuration.</summary>
    UnknownItem,

    /// <summary>The purchase line's loot has no positive buy value.</summary>
    NotPurchasable,

    /// <summary>The purchase line exceeds the merchant's currently available stock.</summary>
    InsufficientStock,

    /// <summary>The sale line exceeds the units owned in the confirmed Inventory.</summary>
    InsufficientOwnedUnits,

    /// <summary>The projected Currency after the whole trade is negative.</summary>
    InsufficientCurrency,

    /// <summary>The projected Inventory after the whole trade occupies more slots than its capacity.</summary>
    CapacityExceeded,

    /// <summary>A Currency value or a projected stack amount exceeds its representable range.</summary>
    Overflow
}
