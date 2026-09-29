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

    public StashOperationResult TryExecuteTrade(ProfileId profileId, MerchantTradeTicket ticket)
    {
        if (_store == null) return StashOperationResult.PersistenceFailed;
        return _store.TryCommitTrade(profileId, ticket);
    }

    public bool IsTradeApplied(ProfileId profileId, ShopTransactionId transactionId)
    {
        return _store != null && _store.IsTradeApplied(profileId, transactionId);
    }
}
