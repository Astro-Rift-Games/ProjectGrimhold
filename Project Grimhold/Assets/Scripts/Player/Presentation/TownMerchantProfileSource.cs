using System;

/// <summary>
/// Confirmed-profile dependencies of the local Town merchant session, supplied by the Town
/// profile composition: confirmed Inventory and Currency reads and the persistence boundary.
/// </summary>
public sealed class TownMerchantProfileSource
{
    public BuybackLedger BuybackLedger { get; }
    public IBuybackService BuybackService { get; }
    public ProfileId ProfileId { get; }
    public IInventoryReadSource Inventory { get; }
    public IPlayerCurrencyService Currency { get; }
    public IShopTransactionService ShopTransactionService { get; }

    public TownMerchantProfileSource(
        ProfileId profileId,
        IInventoryReadSource inventory,
        IPlayerCurrencyService currency,
        IShopTransactionService shopTransactionService,
        BuybackLedger buybackLedger = null,
        IBuybackService buybackService = null)
    {
        if (!profileId.IsValid)
        {
            throw new ArgumentException("The merchant profile must be valid.", nameof(profileId));
        }

        BuybackLedger = buybackLedger;
        BuybackService = buybackService;
        ProfileId = profileId;
        Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        ShopTransactionService = shopTransactionService ?? throw new ArgumentNullException(nameof(shopTransactionService));
    }
}
