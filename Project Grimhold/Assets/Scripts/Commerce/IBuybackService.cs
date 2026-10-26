using System.Threading.Tasks;

/// <summary>Account-local buy-back persistence, independent of shared merchant stock and tickets.</summary>
public interface IBuybackService
{
    Task<StashOperationResult> ExecuteBuybackAsync(ProfileId profileId, MerchantBuybackRequest request);
}
