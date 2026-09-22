/// <summary>Outcome of a persistent prepared-ability mutation.</summary>
public enum AbilityPreparationResult
{
    Success,
    InvalidSlot,
    InvalidAbility,
    UnknownAbility,
    AbilityNotUnlocked,
    DuplicateAbility,
    AttributeRequirementsNotMet,
    InvalidPreparedState,
    ProfileUnavailable,
    PersistenceFailed
}
