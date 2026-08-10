/// <summary>
/// Durable storage boundary for one local profile aggregate.
/// </summary>
public interface ILocalProfileRepository
{
    LocalProfilePersistenceStatus Status { get; }
    string LastError { get; }
    LocalProfileSnapshot Snapshot { get; }

    bool Initialize(ProfileId profileId, LootDefinitionCatalog catalog);
    bool TrySave(LocalProfileSnapshot snapshot, out string error);
}
