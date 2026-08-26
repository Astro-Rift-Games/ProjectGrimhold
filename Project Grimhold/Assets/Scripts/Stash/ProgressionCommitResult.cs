/// <summary>Outcome of attempting one atomic local progression commit.</summary>
public enum ProgressionCommitResult
{
    Success,
    AlreadyApplied,
    PersistenceFailed,
    Stale,
    Conflict,
    Invalid
}
