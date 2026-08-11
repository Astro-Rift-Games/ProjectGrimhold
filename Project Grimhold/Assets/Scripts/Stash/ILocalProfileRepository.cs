/// <summary>
/// Storage boundary for one local profile aggregate.
/// Implementations define whether the snapshot is process-local or durable.
/// </summary>
public interface ILocalProfileRepository
{
    LocalProfilePersistenceStatus Status { get; }
    string LastError { get; }
    LocalProfileSnapshot Snapshot { get; }

    bool Initialize(ProfileId profileId, LootDefinitionCatalog catalog);
    bool TrySave(LocalProfileSnapshot snapshot, out string error);
}
