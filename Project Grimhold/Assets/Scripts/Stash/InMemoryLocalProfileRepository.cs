/// <summary>
/// Owns the local profile aggregate for the lifetime of the current application process.
///
/// It preserves stash, loadout, reservations and extraction receipts across scene and
/// NetworkRunner transitions, but it never reads or writes persistent storage.
/// </summary>
public sealed class InMemoryLocalProfileRepository : ILocalProfileRepository
{
    private readonly object _sync = new();
    private LootDefinitionCatalog _catalog;
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
        _catalog = catalog;
        Snapshot = new LocalProfileSnapshot { ProfileId = profileId };
        Status = LocalProfilePersistenceStatus.Ready;
        LastError = null;
        return true;
    }

    /// <summary>
    /// Replaces the current process snapshot after applying the same aggregate validation
    /// used by the durable repository. No filesystem or PlayerPrefs data is written.
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

            if (!LocalProfileSaveCodec.TryDecode(
                    LocalProfileSaveCodec.Encode(snapshot),
                    _profileId,
                    _catalog,
                    out LocalProfileSnapshot validatedSnapshot,
                    out _,
                    out error))
            {
                LastError = error;
                return false;
            }

            Snapshot = validatedSnapshot;
            LastError = null;
            return true;
        }
    }
}
