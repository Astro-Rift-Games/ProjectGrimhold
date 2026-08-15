using UnityEngine;

/// <summary>
/// Local implementation of the shop transaction service that delegates directly
/// to the single-player LocalProfileStore.
/// </summary>
public sealed class LocalShopTransactionService : MonoBehaviour, IShopTransactionService
{
    private LocalProfileStore _store;

    public void Initialize(LocalProfileStore store)
    {
        _store = store ?? throw new System.ArgumentNullException(nameof(store));
    }

    public StashOperationResult TryExecutePurchase(ProfileId profileId, LootId lootId, int amount, long declaredPrice, ShopTransactionId transactionId)
    {
        if (_store == null) return StashOperationResult.PersistenceFailed;
        if (!IsProfile(profileId)) return StashOperationResult.InvalidInventory;

        var receipt = new ShopTransactionReceipt(transactionId, profileId);
        
        bool isLobby = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Lobby");
        return _store.TryCommitPurchase(receipt, lootId, amount, declaredPrice, isLobby);
    }

    public StashOperationResult TryExecuteSale(ProfileId profileId, LootId lootId, int amount, long declaredSellValue, ShopTransactionId transactionId)
    {
        if (_store == null) return StashOperationResult.PersistenceFailed;
        if (!IsProfile(profileId)) return StashOperationResult.InvalidInventory;

        var receipt = new ShopTransactionReceipt(transactionId, profileId);
        bool isLobby = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Lobby");
        return _store.TryCommitSale(receipt, lootId, amount, declaredSellValue, isLobby);
    }

    private bool IsProfile(ProfileId profileId)
    {
        return profileId.IsValid && _store.ProfileId == profileId;
    }
}
