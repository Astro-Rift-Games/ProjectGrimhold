/// <summary>Application-level result of assigning one character attribute point.</summary>
public enum CharacterAttributeAssignmentCommitResult
{
    Success = 0,
    Unavailable = 1,
    Rejected = 2,
    PersistenceFailed = 3
}
