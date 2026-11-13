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
    PersistenceFailed,
    ReservationPending,
    RevisionConflict,

    /// <summary>The backend no longer retains this transaction id; it was neither applied nor re-applied.</summary>
    TransactionExpired
}
