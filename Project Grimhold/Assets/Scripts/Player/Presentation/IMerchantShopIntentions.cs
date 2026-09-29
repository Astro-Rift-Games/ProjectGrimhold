/// <summary>
/// Trade intentions raised by <see cref="MerchantShopUI"/>. They only edit or confirm the pending
/// trade; none of them mutates the profile or merchant stock directly.
/// </summary>
public interface IMerchantShopIntentions
{
    bool AddPurchase(LootId lootId, int amount);
    bool SetPurchaseAmount(LootId lootId, int amount);
    bool RemovePurchase(LootId lootId);

    bool AddSale(LootId lootId, int amount);
    bool SetSaleAmount(LootId lootId, int amount);
    bool RemoveSale(LootId lootId);

    bool ClearTrade();
    bool ConfirmTrade();
}
