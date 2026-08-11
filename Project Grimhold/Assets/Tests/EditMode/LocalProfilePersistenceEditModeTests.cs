#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public sealed class LocalProfilePersistenceEditModeTests
{
    private const string MainPath = ".\\grimhold-profile.json";
    private const string TemporaryPath = ".\\grimhold-profile.json.tmp";
    private const string BackupPath = ".\\grimhold-profile.json.bak";
    private LootDefinitionCatalog _catalog;

    [SetUp]
    public void SetUp()
    {
        _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        Assert.That(_catalog, Is.Not.Null);
    }

    [Test]
    public void InMemoryRepository_NewInstanceStartsWithEmptyGameplayData()
    {
        var profile = new ProfileId("10101010101010101010101010101010");
        var currentProcess = new InMemoryLocalProfileRepository();
        Assert.That(currentProcess.Initialize(profile, _catalog), Is.True);
        var snapshot = currentProcess.Snapshot.Clone();
        snapshot.Stash.Add(new StashItem(new LootId("coins"), 5));
        Assert.That(currentProcess.TrySave(snapshot, out string error), Is.True, error);
        Assert.That(currentProcess.Snapshot.Stash, Has.Count.EqualTo(1));

        var nextProcess = new InMemoryLocalProfileRepository();
        Assert.That(nextProcess.Initialize(profile, _catalog), Is.True);
        Assert.That(nextProcess.Snapshot.Stash, Is.Empty);
        Assert.That(nextProcess.Snapshot.Loadout, Is.Empty);
        Assert.That(nextProcess.Snapshot.PendingReservation, Is.Null);
        Assert.That(nextProcess.Snapshot.AppliedExtractionReceipts, Is.Empty);
    }

    [Test]
    public void InMemoryRepository_RejectsSnapshotFromAnotherProfile()
    {
        var localProfile = new ProfileId("20202020202020202020202020202020");
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(localProfile, _catalog), Is.True);
        var foreignSnapshot = new LocalProfileSnapshot
        {
            ProfileId = new ProfileId("30303030303030303030303030303030")
        };

        Assert.That(repository.TrySave(foreignSnapshot, out string error), Is.False);
        Assert.That(error, Does.Contain("does not match"));
        Assert.That(repository.Snapshot.ProfileId, Is.EqualTo(localProfile));
    }

    [Test]
    public void Repository_RoundTripsVersionOneSnapshot()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("11111111111111111111111111111111");
        var repository = new LocalProfileRepository(files, ".");

        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var snapshot = repository.Snapshot.Clone();
        snapshot.Stash.Add(new StashItem(new LootId("healthpotion"), 2));
        Assert.That(repository.TrySave(snapshot, out string error), Is.True, error);

        var reloaded = new LocalProfileRepository(files, ".");
        Assert.That(reloaded.Initialize(profile, _catalog), Is.True, reloaded.LastError);
        Assert.That(reloaded.Snapshot.Stash[0].Amount, Is.EqualTo(2));
    }

    [Test]
    public void Codec_RoundTripWithoutPendingReservationKeepsReservationAbsent()
    {
        var profile = new ProfileId("77777777777777777777777777777777");
        var snapshot = new LocalProfileSnapshot { ProfileId = profile };

        bool decoded = LocalProfileSaveCodec.TryDecode(
            LocalProfileSaveCodec.Encode(snapshot),
            profile,
            _catalog,
            out LocalProfileSnapshot restored,
            out LocalProfilePersistenceStatus status,
            out string error);

        Assert.That(decoded, Is.True, error);
        Assert.That(status, Is.EqualTo(LocalProfilePersistenceStatus.Ready));
        Assert.That(restored.PendingReservation, Is.Null);
    }

    [Test]
    public void Store_RepeatedExtractionReceiptDoesNotDuplicateLootOrEvents()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("22222222222222222222222222222222");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, profile);
        int eventCount = 0;
        store.ProfileCommitted += _ => eventCount++;
        var receipt = new ExtractionReceipt("raid-1", profile, 1);
        var items = new[] { new StashItem(new LootId("coins"), 3) };

        Assert.That(store.TryCommitExtraction(receipt, items), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryCommitExtraction(receipt, items), Is.EqualTo(StashOperationResult.AlreadySecured));
        Assert.That(store.GetStash()[0].Amount, Is.EqualTo(3));
        Assert.That(eventCount, Is.EqualTo(1));
    }

    [Test]
    public void Store_ExtractionPersistenceFailurePreservesSnapshotAndRetryCommitsOnce()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("55555555555555555555555555555555");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, profile);
        var receipt = new ExtractionReceipt("raid-failure", profile, 1);
        var items = new[] { new StashItem(new LootId("coins"), 5) };

        files.FailWrites = true;
        Assert.That(store.TryCommitExtraction(receipt, items), Is.EqualTo(StashOperationResult.PersistenceFailed));
        Assert.That(store.GetStash(), Is.Empty);

        files.FailWrites = false;
        Assert.That(store.TryCommitExtraction(receipt, items), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryCommitExtraction(receipt, items), Is.EqualTo(StashOperationResult.AlreadySecured));
        Assert.That(store.GetStash()[0], Is.EqualTo(items[0]));
    }

    [Test]
    public void Store_EmptyExtractionCommitsReceiptWithoutCreatingStashEntries()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("66666666666666666666666666666666");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, profile);
        var receipt = new ExtractionReceipt("raid-empty", profile, 1);

        Assert.That(store.TryCommitExtraction(receipt, Array.Empty<StashItem>()), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryCommitExtraction(receipt, Array.Empty<StashItem>()), Is.EqualTo(StashOperationResult.AlreadySecured));
        Assert.That(store.GetStash(), Is.Empty);
    }

    [Test]
    public void Store_EmptyLoadoutReservationIsDurableAndIdempotent()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("88888888888888888888888888888888");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, profile);
        int eventCount = 0;
        store.ProfileCommitted += _ => eventCount++;

        Assert.That(store.TryCreateLoadoutReservation("reservation-empty", out IReadOnlyList<StashItem> items), Is.EqualTo(StashOperationResult.Success));
        Assert.That(items, Is.Empty);
        Assert.That(store.PendingReservation, Is.Not.Null);
        Assert.That(store.TryCreateLoadoutReservation("reservation-empty", out IReadOnlyList<StashItem> repeated), Is.EqualTo(StashOperationResult.Success));
        Assert.That(repeated, Is.Empty);
        Assert.That(eventCount, Is.EqualTo(1));

        var reloaded = new LocalProfileRepository(files, ".");
        Assert.That(reloaded.Initialize(profile, _catalog), Is.True, reloaded.LastError);
        Assert.That(reloaded.Snapshot.PendingReservation.ReservationId, Is.EqualTo("reservation-empty"));
        Assert.That(reloaded.Snapshot.PendingReservation.Items, Is.Empty);
    }

    [Test]
    public void Store_LoadoutReservationRollbackRestoresExactContentAndConfirmConsumesIt()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("99999999999999999999999999999999");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var snapshot = repository.Snapshot.Clone();
        snapshot.Loadout.Add(new StashItem(new LootId("coins"), 7));
        snapshot.Loadout.Add(new StashItem(new LootId("bone"), 2));
        Assert.That(repository.TrySave(snapshot, out _), Is.True);
        var store = new LocalProfileStore(repository, profile);

        Assert.That(store.TryCreateLoadoutReservation("reservation-1", out IReadOnlyList<StashItem> items), Is.EqualTo(StashOperationResult.Success));
        Assert.That(items, Has.Count.EqualTo(2));
        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.TryRollbackLoadoutReservation("reservation-1"), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetLoadout(), Is.EquivalentTo(new[]
        {
            new StashItem(new LootId("coins"), 7),
            new StashItem(new LootId("bone"), 2)
        }));

        Assert.That(store.TryCreateLoadoutReservation("reservation-2", out _), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryConfirmLoadoutReservation("reservation-2"), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.PendingReservation, Is.Null);
        Assert.That(store.GetLoadout(), Is.Empty);
    }

    [Test]
    public void Repository_UsesValidBackupWhenMainIsMalformed()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("33333333333333333333333333333333");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var snapshot = repository.Snapshot.Clone();
        snapshot.Stash.Add(new StashItem(new LootId("bone"), 4));
        Assert.That(repository.TrySave(snapshot, out _), Is.True);
        files.Files[BackupPath] = files.Files[MainPath];
        files.Files[MainPath] = "{ malformed";

        var recovered = new LocalProfileRepository(files, ".");
        Assert.That(recovered.Initialize(profile, _catalog), Is.True);
        Assert.That(recovered.Status, Is.EqualTo(LocalProfilePersistenceStatus.RecoveredFromBackup));
        Assert.That(recovered.Snapshot.Stash[0].Amount, Is.EqualTo(4));
    }

    [Test]
    public void Codec_RejectsFutureSchema()
    {
        const string json = "{\"schemaVersion\":99,\"profileId\":\"44444444444444444444444444444444\"}";
        Assert.That(LocalProfileSaveCodec.TryDecode(
            json,
            new ProfileId("44444444444444444444444444444444"),
            _catalog,
            out _,
            out LocalProfilePersistenceStatus status,
            out _), Is.False);
        Assert.That(status, Is.EqualTo(LocalProfilePersistenceStatus.UnsupportedVersion));
    }

    private sealed class MemoryFileStore : ILocalProfileFileStore
    {
        public readonly Dictionary<string, string> Files = new(StringComparer.Ordinal);
        public bool FailWrites;

        public bool Exists(string path) => Files.ContainsKey(path);
        public bool TryRead(string path, out string contents, out string error)
        {
            if (Files.TryGetValue(path, out contents)) { error = null; return true; }
            error = "Missing file";
            return false;
        }

        public bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath, string contents, out string error)
        {
            if (FailWrites)
            {
                error = "Simulated disk failure";
                return false;
            }

            if (Files.TryGetValue(mainPath, out string previous)) Files[backupPath] = previous;
            Files[temporaryPath] = contents;
            Files[mainPath] = Files[temporaryPath];
            Files.Remove(temporaryPath);
            error = null;
            return true;
        }

        public bool TryRestoreMainFromBackup(string mainPath, string backupPath, out string error)
        {
            if (!Files.TryGetValue(backupPath, out string backup)) { error = "Missing backup"; return false; }
            Files[mainPath] = backup;
            error = null;
            return true;
        }
    }
}
#endif
