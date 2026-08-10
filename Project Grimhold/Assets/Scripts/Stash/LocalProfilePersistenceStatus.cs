/// <summary>
/// Describes the availability of the local profile aggregate.
/// </summary>
public enum LocalProfilePersistenceStatus
{
    Ready,
    RecoveredFromBackup,
    Unavailable,
    UnsupportedVersion
}
