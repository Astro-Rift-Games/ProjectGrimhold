/// <summary>
/// Service abstraction for executing atomic shop transactions on a local profile.
/// Enables future replacement with authoritative server validation.
/// </summary>
public interface IShopTransactionService
{
    /// <summary>
    /// Attempts to execute a purchase, deducting currency and securing loot.
    /// </summary>
    StashOperationResult TryExecutePurchase(ProfileId profileId, LootId lootId, int amount, long declaredPrice, ShopTransactionId transactionId);

    /// <summary>
    /// Attempts to execute a sale, removing loot and crediting currency.
    /// </summary>
    StashOperationResult TryExecuteSale(ProfileId profileId, LootId lootId, int amount, long declaredSellValue, ShopTransactionId transactionId);
}
