/// <summary>
/// Player-facing messages of an Ability Vendor purchase attempt. They name the outcome of
/// <see cref="LocalProfileStore.TryPurchaseAbility"/> and are never read back as domain state.
/// </summary>
public static class AbilityVendorFeedback
{
    public static string Describe(AbilityPurchaseResult result) => result switch
    {
        AbilityPurchaseResult.Success => "Ability learned",
        AbilityPurchaseResult.AlreadyUnlocked => "You already know this ability",
        AbilityPurchaseResult.InsufficientCurrency => "Not enough gold",
        AbilityPurchaseResult.InvalidAbility => "This ability is not for sale",
        AbilityPurchaseResult.UnknownAbility => "This ability is not for sale",
        AbilityPurchaseResult.InvalidPrice => "This ability is not for sale",
        AbilityPurchaseResult.ProfileUnavailable => "Your profile is not available",
        AbilityPurchaseResult.PersistenceFailed => "The purchase could not be saved",
        _ => "Purchase not available"
    };
}
