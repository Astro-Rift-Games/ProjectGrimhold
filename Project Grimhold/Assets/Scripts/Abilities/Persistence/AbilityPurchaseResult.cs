/// <summary>Outcome of buying an ability unlock with Currency.</summary>
public enum AbilityPurchaseResult
{
    Success,
    AlreadyUnlocked,
    InvalidAbility,
    UnknownAbility,
    InvalidPrice,
    InsufficientCurrency,
    ProfileUnavailable,
    PersistenceFailed
}
