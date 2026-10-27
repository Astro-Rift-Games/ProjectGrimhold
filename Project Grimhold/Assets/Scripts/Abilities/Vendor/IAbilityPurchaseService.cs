using System.Threading.Tasks;

/// <summary>
/// Authoritative application-level service boundary for purchasing ability unlocks with Currency.
/// </summary>
public interface IAbilityPurchaseService
{
    Task<AbilityPurchaseResult> PurchaseAbilityAsync(ProfileId profileId, AbilityId abilityId, long price);
}
