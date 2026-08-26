/// <summary>Deterministic reason why eligible Raid loot could not be converted to experience.</summary>
public enum ExtractedLootExperienceCalculationFailure : byte
{
    None = 0,
    MissingEligibilitySnapshot = 1,
    MissingValueSource = 2,
    InvalidPercentage = 3,
    InvalidEligibilityEntry = 4,
    DuplicateLootId = 5,
    InconsistentEligibilityTotals = 6,
    MissingOrInvalidValue = 7,
    ValueMultiplicationOverflow = 8,
    EligibleValueOverflow = 9,
    PercentageOverflow = 10
}
