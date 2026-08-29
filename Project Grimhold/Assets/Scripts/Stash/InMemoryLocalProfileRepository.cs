/// <summary>
/// Owns the local profile aggregate for the lifetime of the current application process.
///
/// It preserves stash, loadout, reservations and extraction receipts across scene and
/// NetworkRunner transitions, but it never reads or writes persistent storage.
/// </summary>
public sealed class InMemoryLocalProfileRepository : ILocalProfileRepository
{
    private readonly object _sync = new();
    private ProfileId _profileId;

    public LocalProfilePersistenceStatus Status { get; private set; } = LocalProfilePersistenceStatus.Unavailable;
    public string LastError { get; private set; }
    public LocalProfileSnapshot Snapshot { get; private set; }

    /// <summary>
    /// Starts an empty profile aggregate for this application process.
    /// Existing files and state from previous processes are intentionally ignored.
    /// </summary>
    public bool Initialize(ProfileId profileId, LootDefinitionCatalog catalog)
    {
        if (!profileId.IsValid || catalog == null)
        {
            Status = LocalProfilePersistenceStatus.Unavailable;
            LastError = "Local profile identity or loot catalog is missing.";
            Snapshot = null;
            return false;
        }

        _profileId = profileId;
        Snapshot = new LocalProfileSnapshot { ProfileId = profileId };
        Status = LocalProfilePersistenceStatus.Ready;
        LastError = null;
        return true;
    }

    /// <summary>
    /// Replaces the current process snapshot with an isolated clone of the accepted aggregate.
    /// Domain mutations are validated by LocalProfileStore and its pure rules; this productive
    /// repository never routes state through the legacy JSON codec.
    /// </summary>
    public bool TrySave(LocalProfileSnapshot snapshot, out string error)
    {
        lock (_sync)
        {
            error = null;
            if (Status != LocalProfilePersistenceStatus.Ready)
            {
                error = LastError ?? "The in-memory profile is unavailable.";
                return false;
            }

            if (snapshot == null || snapshot.ProfileId != _profileId)
            {
                error = "Snapshot profile ID does not match the initialized local profile.";
                return false;
            }

            Snapshot = snapshot.Clone();
            LastError = null;
            return true;
        }
    }
}
