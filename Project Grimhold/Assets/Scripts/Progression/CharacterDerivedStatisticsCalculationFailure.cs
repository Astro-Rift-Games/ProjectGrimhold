/// <summary>Reason a character-derived-statistics calculation could not produce a complete result.</summary>
public enum CharacterDerivedStatisticsCalculationFailure : byte
{
    None = 0,
    MissingConfiguration = 1,
    MaximumHealthOverflow = 2,
    MaximumStaminaOverflow = 3
}
