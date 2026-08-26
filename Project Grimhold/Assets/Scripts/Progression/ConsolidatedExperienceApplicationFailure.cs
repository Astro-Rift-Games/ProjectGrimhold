/// <summary>Deterministic reason why consolidated experience was not applied.</summary>
public enum ConsolidatedExperienceApplicationFailure : byte
{
    None = 0,
    AlreadyApplied = 1,
    UnresolvedResolution = 2,
    InvalidProgressionState = 3
}
