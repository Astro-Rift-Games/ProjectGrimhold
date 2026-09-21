#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

[Category("TASK419")]
public sealed class UnlockedAbilityRepertoireTests
{
    private const string AbilityCatalogPath =
        "Assets/Scriptable Objects/Abilities/Catalogs/AbilityDefinitionCatalog.asset";

    private static readonly ProfileId Profile =
        new("92929292929292929292929292929292");

    private AbilityDefinitionCatalog _abilityCatalog;
    private LootDefinitionCatalog _lootCatalog;

    [SetUp]
    public void SetUp()
    {
        _abilityCatalog = AssetDatabase.LoadAssetAtPath<AbilityDefinitionCatalog>(AbilityCatalogPath);
        _lootCatalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");

        Assert.That(_abilityCatalog, Is.Not.Null);
        Assert.That(_lootCatalog, Is.Not.Null);
    }

    [Test]
    public void Unlock_ValidAbilityCommitsAndPublishesOnce()
    {
        LocalProfileStore store = CreateStore(out InMemoryLocalProfileRepository repository);
        AbilityId abilityId = new("charge");
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        AbilityUnlockResult result = store.TryUnlockAbility(abilityId);

        Assert.That(result, Is.EqualTo(AbilityUnlockResult.Success));
        Assert.That(store.IsAbilityUnlocked(abilityId), Is.True);
        Assert.That(store.GetUnlockedAbilities(), Is.EqualTo(new[] { abilityId }));
        Assert.That(repository.Snapshot.UnlockedAbilities, Is.EqualTo(new[] { abilityId }));
        Assert.That(commits, Is.EqualTo(1));
    }

    [Test]
    public void Unlock_DuplicateIsIdempotentAndDoesNotPublishAgain()
    {
        LocalProfileStore store = CreateStore(out _);
        AbilityId abilityId = new("charge");
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        Assert.That(store.TryUnlockAbility(abilityId), Is.EqualTo(AbilityUnlockResult.Success));
        Assert.That(store.TryUnlockAbility(abilityId), Is.EqualTo(AbilityUnlockResult.AlreadyUnlocked));

        Assert.That(store.GetUnlockedAbilities(), Is.EqualTo(new[] { abilityId }));
        Assert.That(commits, Is.EqualTo(1));
    }

    [Test]
    public void Unlock_UnknownAbilityPreservesSnapshotAndDoesNotPublish()
    {
        LocalProfileStore store = CreateStore(out InMemoryLocalProfileRepository repository);
        LocalProfileSnapshot before = repository.Snapshot;
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        AbilityUnlockResult result = store.TryUnlockAbility(new AbilityId("unknown_ability"));

        Assert.That(result, Is.EqualTo(AbilityUnlockResult.UnknownAbility));
        Assert.That(repository.Snapshot, Is.SameAs(before));
        Assert.That(store.GetUnlockedAbilities(), Is.Empty);
        Assert.That(commits, Is.Zero);
    }

    [Test]
    public void Unlock_PersistenceFailurePreservesSnapshotAndDoesNotPublish()
    {
        var repository = new RejectingRepository(new LocalProfileSnapshot { ProfileId = Profile });
        var store = new LocalProfileStore(
            repository,
            Profile,
            abilityCatalog: _abilityCatalog);
        LocalProfileSnapshot before = repository.Snapshot;
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        LogAssert.Expect(LogType.Error, new Regex("\\[LocalProfileStore\\] Commit failed\\."));

        AbilityUnlockResult result = store.TryUnlockAbility(new AbilityId("charge"));

        Assert.That(result, Is.EqualTo(AbilityUnlockResult.PersistenceFailed));
        Assert.That(repository.Snapshot, Is.SameAs(before));
        Assert.That(repository.Snapshot.UnlockedAbilities, Is.Empty);
        Assert.That(commits, Is.Zero);
    }

    [Test]
    public void SnapshotClone_CopiesUnlockedAbilitiesWithoutSharingTheCollection()
    {
        AbilityId charge = new("charge");
        AbilityId trap = new("trap");
        var snapshot = new LocalProfileSnapshot { ProfileId = Profile };
        snapshot.UnlockedAbilities.Add(charge);

        LocalProfileSnapshot clone = snapshot.Clone();
        clone.UnlockedAbilities.Add(trap);

        Assert.That(snapshot.UnlockedAbilities, Is.EqualTo(new[] { charge }));
        Assert.That(clone.UnlockedAbilities, Is.EqualTo(new[] { charge, trap }));
    }

    [Test]
    public void Codec_RoundTripsUnlockedAbilities()
    {
        var snapshot = new LocalProfileSnapshot { ProfileId = Profile };
        snapshot.UnlockedAbilities.Add(new AbilityId("charge"));
        snapshot.UnlockedAbilities.Add(new AbilityId("trap"));

        bool decoded = LocalProfileSaveCodec.TryDecode(
            LocalProfileSaveCodec.Encode(snapshot),
            Profile,
            _lootCatalog,
            out LocalProfileSnapshot restored,
            out LocalProfilePersistenceStatus status,
            out string error);

        Assert.That(decoded, Is.True, error);
        Assert.That(status, Is.EqualTo(LocalProfilePersistenceStatus.Ready));
        Assert.That(
            restored.UnlockedAbilities,
            Is.EqualTo(new[] { new AbilityId("charge"), new AbilityId("trap") }));
    }

    [Test]
    public void Codec_PreviousSchemaInitializesEmptyRepertoire()
    {
        var snapshot = new LocalProfileSnapshot { ProfileId = Profile };
        snapshot.UnlockedAbilities.Add(new AbilityId("charge"));
        string legacyJson = LocalProfileSaveCodec.Encode(snapshot)
            .Replace(
                $"\"schemaVersion\": {LocalProfileSnapshot.CurrentSchemaVersion}",
                $"\"schemaVersion\": {LocalProfileSnapshot.CurrentSchemaVersion - 1}")
            .Replace("\"unlockedAbilityIds\"", "\"legacyUnlockedAbilityIds\"");

        bool decoded = LocalProfileSaveCodec.TryDecode(
            legacyJson,
            Profile,
            _lootCatalog,
            out LocalProfileSnapshot restored,
            out _,
            out string error);

        Assert.That(decoded, Is.True, error);
        Assert.That(restored.SchemaVersion, Is.EqualTo(LocalProfileSnapshot.CurrentSchemaVersion));
        Assert.That(restored.UnlockedAbilities, Is.Empty);
    }

    [Test]
    public void Codec_DuplicateAbilityIdsAreRejected()
    {
        var snapshot = new LocalProfileSnapshot { ProfileId = Profile };
        snapshot.UnlockedAbilities.Add(new AbilityId("charge"));
        snapshot.UnlockedAbilities.Add(new AbilityId("charge"));

        bool decoded = LocalProfileSaveCodec.TryDecode(
            LocalProfileSaveCodec.Encode(snapshot),
            Profile,
            _lootCatalog,
            out _,
            out _,
            out string error);

        Assert.That(decoded, Is.False);
        Assert.That(error, Does.Contain("duplicate"));
    }

    [Test]
    public void Configuration_ProvidesTheAuthoritativeAbilityCatalog()
    {
        LocalProfilePersistenceConfiguration configuration =
            AssetDatabase.LoadAssetAtPath<LocalProfilePersistenceConfiguration>(
                "Assets/Resources/LocalProfilePersistenceConfiguration.asset");

        Assert.That(configuration, Is.Not.Null);
        Assert.That(configuration.AbilityCatalog, Is.SameAs(_abilityCatalog));
        Assert.That(configuration.AbilityCatalog.TryValidate(out string error), Is.True, error);
    }

    private LocalProfileStore CreateStore(out InMemoryLocalProfileRepository repository)
    {
        repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(Profile, _lootCatalog), Is.True);
        return new LocalProfileStore(
            repository,
            Profile,
            lootCatalog: _lootCatalog,
            abilityCatalog: _abilityCatalog);
    }

    private sealed class RejectingRepository : ILocalProfileRepository
    {
        public RejectingRepository(LocalProfileSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public LocalProfilePersistenceStatus Status => LocalProfilePersistenceStatus.Ready;
        public string LastError => "Simulated persistence failure.";
        public LocalProfileSnapshot Snapshot { get; }

        public bool Initialize(ProfileId profileId, LootDefinitionCatalog catalog) => false;

        public bool TrySave(LocalProfileSnapshot snapshot, out string error)
        {
            error = LastError;
            return false;
        }
    }
}
#endif
