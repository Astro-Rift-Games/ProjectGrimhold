/// <summary>Deterministic reason why character attribute points were not granted.</summary>
public enum CharacterAttributePointGrantFailure : byte
{
    None = 0,
    AlreadyApplied = 1,
    InvalidProgressionResult = 2,
    InvalidPointsPerLevel = 3,
    AvailablePointsOverflow = 4
}
