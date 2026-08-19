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
    public void Codec_RoundTripsPreparedWeaponsInLoadoutAndReservation()
    {
        var profile = new ProfileId("17171717171717171717171717171717");
        LootId sword = new("training_sword");
        var active = new LocalProfileSnapshot { ProfileId = profile };
        active.Loadout.Add(new StashItem(sword, 2));
        active.PreparedWeapons = new PreparedWeaponLoadout(sword, sword);

        Assert.That(LocalProfileSaveCodec.TryDecode(
            LocalProfileSaveCodec.Encode(active),
            profile,
            _catalog,
            out LocalProfileSnapshot restoredActive,
            out _,
            out string activeError), Is.True, activeError);
        Assert.That(restoredActive.PreparedWeapons.WeaponSlot1, Is.EqualTo(sword));
        Assert.That(restoredActive.PreparedWeapons.WeaponSlot2, Is.EqualTo(sword));

        var pending = new LocalProfileSnapshot { ProfileId = profile };
        pending.PendingReservation = new PendingLoadoutReservation(
            "reservation-codec",
            new[] { new StashItem(sword, 2) },
            new PreparedWeaponLoadout(sword, sword));

        Assert.That(LocalProfileSaveCodec.TryDecode(
            LocalProfileSaveCodec.Encode(pending),
            profile,
            _catalog,
            out LocalProfileSnapshot restoredPending,
            out _,
            out string pendingError), Is.True, pendingError);
        Assert.That(restoredPending.PendingReservation.PreparedWeapons.WeaponSlot1, Is.EqualTo(sword));
        Assert.That(restoredPending.PendingReservation.PreparedWeapons.WeaponSlot2, Is.EqualTo(sword));
    }

    [Test]
    public void Store_RepeatedExtractionReceiptDoesNotDuplicateLootOrEvents()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("22222222222222222222222222222222");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, profile);
        var initial = repository.Snapshot.Clone();
        initial.Stash.Add(new StashItem(new LootId("bone"), 2));
        Assert.That(repository.TrySave(initial, out string initialError), Is.True, initialError);
        int eventCount = 0;
        store.ProfileCommitted += _ => eventCount++;
        var receipt = new ExtractionReceipt("raid-1", profile, 1);
        var items = new[] { new StashItem(new LootId("coins"), 3) };

        Assert.That(store.TryCommitExtraction(receipt, items), Is.EqualTo(StashOperationResult.Success));
        CollectionAssert.AreEqual(items, store.GetLoadout());
        CollectionAssert.AreEqual(new[] { new StashItem(new LootId("bone"), 2) }, store.GetStash());

        // The duplicate must be recognized before inspecting the current Loadout.
        Assert.That(store.TryCommitExtraction(receipt, items), Is.EqualTo(StashOperationResult.AlreadySecured));
        CollectionAssert.AreEqual(items, store.GetLoadout());
        CollectionAssert.AreEqual(new[] { new StashItem(new LootId("bone"), 2) }, store.GetStash());
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
        var initial = repository.Snapshot.Clone();
        initial.Stash.Add(new StashItem(new LootId("bone"), 2));
        Assert.That(repository.TrySave(initial, out string initialError), Is.True, initialError);
        var receipt = new ExtractionReceipt("raid-failure", profile, 1);
        var items = new[] { new StashItem(new LootId("coins"), 5) };

        files.FailWrites = true;
        Assert.That(store.TryCommitExtraction(receipt, items), Is.EqualTo(StashOperationResult.PersistenceFailed));
        Assert.That(store.GetLoadout(), Is.Empty);
        CollectionAssert.AreEqual(new[] { new StashItem(new LootId("bone"), 2) }, store.GetStash());
        Assert.That(repository.Snapshot.AppliedExtractionReceipts, Is.Empty);

        files.FailWrites = false;
        Assert.That(store.TryCommitExtraction(receipt, items), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryCommitExtraction(receipt, items), Is.EqualTo(StashOperationResult.AlreadySecured));
        CollectionAssert.AreEqual(items, store.GetLoadout());
        CollectionAssert.AreEqual(new[] { new StashItem(new LootId("bone"), 2) }, store.GetStash());
        Assert.That(repository.Snapshot.AppliedExtractionReceipts, Has.Count.EqualTo(1));
    }

    [Test]
    public void Store_EmptyExtractionCommitsReceiptWithoutCreatingInventoryEntries()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("66666666666666666666666666666666");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, profile);
        var receipt = new ExtractionReceipt("raid-empty", profile, 1);

        Assert.That(store.TryCommitExtraction(receipt, Array.Empty<StashItem>()), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryCommitExtraction(receipt, Array.Empty<StashItem>()), Is.EqualTo(StashOperationResult.AlreadySecured));
        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.GetStash(), Is.Empty);
        Assert.That(repository.Snapshot.AppliedExtractionReceipts, Has.Count.EqualTo(1));
    }

    [Test]
    public void Store_NewExtractionRejectsUnexpectedLoadoutWithoutChangingProfile()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("12121212121212121212121212121212");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var initial = repository.Snapshot.Clone();
        initial.Loadout.Add(new StashItem(new LootId("coins"), 7));
        initial.Stash.Add(new StashItem(new LootId("bone"), 2));
        Assert.That(repository.TrySave(initial, out string initialError), Is.True, initialError);
        var store = new LocalProfileStore(repository, profile);
        var receipt = new ExtractionReceipt("raid-non-empty-loadout", profile, 1);

        Assert.That(
            store.TryCommitExtraction(receipt, new[] { new StashItem(new LootId("healthpotion"), 1) }),
            Is.EqualTo(StashOperationResult.PersistenceFailed));
        CollectionAssert.AreEqual(new[] { new StashItem(new LootId("coins"), 7) }, store.GetLoadout());
        CollectionAssert.AreEqual(new[] { new StashItem(new LootId("bone"), 2) }, store.GetStash());
        Assert.That(repository.Snapshot.AppliedExtractionReceipts, Is.Empty);
    }

    [Test]
    public void Store_ExtractionBeyondLoadoutCapacityFailsWithoutPartialCommit()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("13131313131313131313131313131313");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, profile);
        var items = new List<StashItem>();
        for (int index = 0; index <= LocalProfileSnapshot.MaxLoadoutSlots; index++)
        {
            items.Add(new StashItem(new LootId($"extracted-{index}"), 1));
        }

        Assert.That(
            store.TryCommitExtraction(new ExtractionReceipt("raid-over-capacity", profile, 1), items),
            Is.EqualTo(StashOperationResult.PersistenceFailed));
        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.GetStash(), Is.Empty);
        Assert.That(repository.Snapshot.AppliedExtractionReceipts, Is.Empty);
    }

    [Test]
    public void Store_ExtractedLoadoutCanBeReservedForTheNextRaid()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("14141414141414141414141414141414");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, profile, _catalog);
        var items = new[]
        {
            new StashItem(new LootId("training_sword"), 1),
            new StashItem(new LootId("bone"), 2)
        };
        Assert.That(
            store.TryCommitExtraction(new ExtractionReceipt("raid-next", profile, 1), items),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(
            store.TryAssignPreparedWeapon(WeaponSlot.Slot1, new LootId("training_sword")),
            Is.EqualTo(StashOperationResult.Success));

        Assert.That(
            store.TryCreateLoadoutReservation("next-raid", out PendingLoadoutReservation reservation),
            Is.EqualTo(StashOperationResult.Success));
        CollectionAssert.AreEqual(items, reservation.Items);
        Assert.That(reservation.PreparedWeapons.WeaponSlot1, Is.EqualTo(new LootId("training_sword")));
        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.PendingReservation.ReservationId, Is.EqualTo("next-raid"));
    }

    [Test]
    public void Store_EmptyLoadoutReservationIsRejectedWithoutMutation()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("88888888888888888888888888888888");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, profile);
        int eventCount = 0;
        store.ProfileCommitted += _ => eventCount++;

        Assert.That(
            store.TryCreateLoadoutReservation("reservation-empty", out PendingLoadoutReservation reservation),
            Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(reservation, Is.Null);
        Assert.That(store.PendingReservation, Is.Null);
        Assert.That(eventCount, Is.Zero);

        var reloaded = new LocalProfileRepository(files, ".");
        Assert.That(reloaded.Initialize(profile, _catalog), Is.True, reloaded.LastError);
        Assert.That(reloaded.Snapshot.PendingReservation, Is.Null);
    }

    [Test]
    public void Store_SameWeaponInBothSlotsRequiresTwoOwnedUnits()
    {
        var profile = new ProfileId("15151515151515151515151515151515");
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, profile, _catalog);
        LootId sword = new("training_sword");

        Assert.That(
            store.TryImportItems(new[] { new StashItem(sword, 1) }),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryAssignPreparedWeapon(WeaponSlot.Slot1, sword), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryAssignPreparedWeapon(WeaponSlot.Slot2, sword), Is.EqualTo(StashOperationResult.InvalidInventory));

        Assert.That(
            store.TryImportItems(new[] { new StashItem(sword, 1) }),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryAssignPreparedWeapon(WeaponSlot.Slot2, sword), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetPreparedWeapons().WeaponSlot1, Is.EqualTo(sword));
        Assert.That(store.GetPreparedWeapons().WeaponSlot2, Is.EqualTo(sword));
    }

    [Test]
    public void Store_ReducingLoadoutReconcilesPreparedWeaponAssignments()
    {
        var profile = new ProfileId("16161616161616161616161616161616");
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var store = new LocalProfileStore(repository, profile, _catalog);
        LootId sword = new("training_sword");
        Assert.That(
            store.TryImportItems(new[] { new StashItem(sword, 2) }),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryAssignPreparedWeapon(WeaponSlot.Slot1, sword), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryAssignPreparedWeapon(WeaponSlot.Slot2, sword), Is.EqualTo(StashOperationResult.Success));

        Assert.That(store.TryTransferToStash(sword, 1), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetPreparedWeapons().WeaponSlot1, Is.EqualTo(sword));
        Assert.That(store.GetPreparedWeapons().HasWeaponSlot2, Is.False);

        Assert.That(store.TryTransferToStash(sword, 1), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetPreparedWeapons().HasAnyWeapon, Is.False);
    }

    // Mirrors LocalProfilePersistenceConfiguration.RecoveryWeaponLootId.
    private const string ConfiguredRecoveryWeapon = "recovery_sword";

    private LocalProfileStore CreatePreparedStore(
        string profileValue,
        out InMemoryLocalProfileRepository repository,
        bool withRecoveryPolicy)
    {
        var profile = new ProfileId(profileValue);
        repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        return new LocalProfileStore(
            repository,
            profile,
            _catalog,
            withRecoveryPolicy ? new LootId(ConfiguredRecoveryWeapon) : default);
    }

    [Test]
    public void Configuration_ShippedRecoveryWeaponResolvesToAUsableWeapon()
    {
        var configuration = AssetDatabase.LoadAssetAtPath<LocalProfilePersistenceConfiguration>(
            "Assets/Resources/LocalProfilePersistenceConfiguration.asset");
        Assert.That(configuration, Is.Not.Null);
        Assert.That(configuration.RecoveryWeaponLootId.Value, Is.EqualTo(ConfiguredRecoveryWeapon));
        Assert.That(
            PreparedWeaponLoadout.IsUsableWeaponDefinition(
                configuration.RecoveryWeaponLootId,
                configuration.LootCatalog),
            Is.True,
            "Town cannot guarantee a recovery weapon that the catalog does not resolve.");
    }

    [Test]
    public void Store_PreparationKeepsAValidEffectiveWeaponUntouched()
    {
        LocalProfileStore store = CreatePreparedStore("41414141414141414141414141414141", out _, true);
        LootId sword = new(ConfiguredRecoveryWeapon);
        Assert.That(store.TryImportItems(new[] { new StashItem(sword, 1) }), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryAssignPreparedWeapon(WeaponSlot.Slot1, sword), Is.EqualTo(StashOperationResult.Success));
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        Assert.That(store.TryPrepareExpeditionWeapons(), Is.EqualTo(ExpeditionPreparationResult.Success));

        Assert.That(commits, Is.Zero, "An already prepared profile must not commit.");
        Assert.That(store.GetLoadout(), Is.EquivalentTo(new[] { new StashItem(sword, 1) }));
        Assert.That(store.GetPreparedWeapons().WeaponSlot1, Is.EqualTo(sword));
    }

    [Test]
    public void Store_PreparationNormalizesSelectionTowardsTheOnlyOccupiedSlot()
    {
        LocalProfileStore store = CreatePreparedStore("42424242424242424242424242424242", out _, true);
        LootId sword = new(ConfiguredRecoveryWeapon);
        Assert.That(store.TryImportItems(new[] { new StashItem(sword, 1) }), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryAssignPreparedWeapon(WeaponSlot.Slot2, sword), Is.EqualTo(StashOperationResult.Success));

        Assert.That(store.TryPrepareExpeditionWeapons(), Is.EqualTo(ExpeditionPreparationResult.Success));

        Assert.That(store.GetPreparedWeapons().WeaponSlot1, Is.EqualTo(sword));
        Assert.That(store.GetPreparedWeapons().HasWeaponSlot2, Is.False);
        // No ownership moved: the single owned unit is still the only one.
        Assert.That(store.GetLoadout(), Is.EquivalentTo(new[] { new StashItem(sword, 1) }));
    }

    [Test]
    public void Store_PreparationWithoutRecoveryConfigurationIsRejectedWithoutMutation()
    {
        LocalProfileStore store = CreatePreparedStore("43434343434343434343434343434343", out _, false);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        Assert.That(
            store.TryPrepareExpeditionWeapons(),
            Is.EqualTo(ExpeditionPreparationResult.RecoveryWeaponUnavailable));

        Assert.That(commits, Is.Zero);
        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.GetPreparedWeapons().HasAnyWeapon, Is.False);
        // The domain invariant still rejects the reservation.
        Assert.That(
            store.TryCreateLoadoutReservation("no-weapon", out PendingLoadoutReservation reservation),
            Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(reservation, Is.Null);
    }

    [Test]
    public void Store_PreparationGrantsTheConfiguredRecoveryWeaponExactlyOnce()
    {
        LocalProfileStore store = CreatePreparedStore("44444444444444444444444444444444", out _, true);
        LootId recovery = new(ConfiguredRecoveryWeapon);

        Assert.That(store.TryPrepareExpeditionWeapons(), Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(store.GetLoadout(), Is.EquivalentTo(new[] { new StashItem(recovery, 1) }));
        Assert.That(store.GetPreparedWeapons().WeaponSlot1, Is.EqualTo(recovery));

        int commits = 0;
        store.ProfileCommitted += _ => commits++;
        Assert.That(store.TryPrepareExpeditionWeapons(), Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(store.TryPrepareExpeditionWeapons(), Is.EqualTo(ExpeditionPreparationResult.Success));

        Assert.That(commits, Is.Zero, "Retrying a launch must not re-grant the recovery weapon.");
        Assert.That(store.GetLoadout(), Is.EquivalentTo(new[] { new StashItem(recovery, 1) }));
    }

    [Test]
    public void Store_PreparationReusesAnOwnedRecoveryUnitBeforeGrantingAnother()
    {
        LocalProfileStore store = CreatePreparedStore("45454545454545454545454545454545", out _, true);
        LootId recovery = new(ConfiguredRecoveryWeapon);
        Assert.That(store.TryImportItems(new[] { new StashItem(recovery, 1) }), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryTransferToStash(recovery, 1), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetStash(), Is.EquivalentTo(new[] { new StashItem(recovery, 1) }));

        Assert.That(store.TryPrepareExpeditionWeapons(), Is.EqualTo(ExpeditionPreparationResult.Success));

        Assert.That(store.GetLoadout(), Is.EquivalentTo(new[] { new StashItem(recovery, 1) }));
        Assert.That(store.GetStash(), Is.Empty, "The owned unit moved instead of a new one being granted.");
    }

    [Test]
    public void Store_PreparationFailsExplicitlyOnAnInvalidPersistedReference()
    {
        LocalProfileStore store = CreatePreparedStore(
            "46464646464646464646464646464646",
            out InMemoryLocalProfileRepository repository,
            true);
        LocalProfileSnapshot corrupted = repository.Snapshot.Clone();
        corrupted.Loadout.Add(new StashItem(new LootId("bone"), 1));
        // "bone" is owned but is not a Weapon: a reference no preparation may silently overwrite.
        corrupted.PreparedWeapons = new PreparedWeaponLoadout(new LootId("bone"), default);
        Assert.That(repository.TrySave(corrupted, out string error), Is.True, error);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;

        Assert.That(
            store.TryPrepareExpeditionWeapons(),
            Is.EqualTo(ExpeditionPreparationResult.InvalidPreparedWeapon));

        Assert.That(commits, Is.Zero);
        Assert.That(store.GetPreparedWeapons().WeaponSlot1, Is.EqualTo(new LootId("bone")));
        Assert.That(store.GetLoadout(), Is.EquivalentTo(new[] { new StashItem(new LootId("bone"), 1) }));
    }

    [Test]
    public void Store_PreparationKeepsBothSlotsAndQuantitiesForTheSameWeapon()
    {
        LocalProfileStore store = CreatePreparedStore("47474747474747474747474747474747", out _, true);
        LootId sword = new(ConfiguredRecoveryWeapon);
        Assert.That(store.TryImportItems(new[] { new StashItem(sword, 2) }), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryAssignPreparedWeapon(WeaponSlot.Slot1, sword), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryAssignPreparedWeapon(WeaponSlot.Slot2, sword), Is.EqualTo(StashOperationResult.Success));

        Assert.That(store.TryPrepareExpeditionWeapons(), Is.EqualTo(ExpeditionPreparationResult.Success));

        Assert.That(store.GetPreparedWeapons().WeaponSlot1, Is.EqualTo(sword));
        Assert.That(store.GetPreparedWeapons().WeaponSlot2, Is.EqualTo(sword));
        Assert.That(store.GetLoadout(), Is.EquivalentTo(new[] { new StashItem(sword, 2) }));
    }

    [Test]
    public void Store_PreparedRecoveryProfileProducesAReservationCarryingInventoryAndEquipment()
    {
        LocalProfileStore store = CreatePreparedStore("48484848484848484848484848484848", out _, true);
        LootId recovery = new(ConfiguredRecoveryWeapon);
        Assert.That(store.TryImportItems(new[] { new StashItem(new LootId("bone"), 3) }), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryPrepareExpeditionWeapons(), Is.EqualTo(ExpeditionPreparationResult.Success));

        Assert.That(
            store.TryCreateLoadoutReservation("prepared-raid", out PendingLoadoutReservation reservation),
            Is.EqualTo(StashOperationResult.Success));

        Assert.That(reservation.Items, Is.EquivalentTo(new[]
        {
            new StashItem(new LootId("bone"), 3),
            new StashItem(recovery, 1)
        }));
        Assert.That(reservation.PreparedWeapons.WeaponSlot1, Is.EqualTo(recovery));
        Assert.That(reservation.PreparedWeapons.HasWeaponSlot2, Is.False);
        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.GetPreparedWeapons().HasAnyWeapon, Is.False);

        // Preparation is inert while a reservation owns the equipment.
        Assert.That(store.TryPrepareExpeditionWeapons(), Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(store.GetLoadout(), Is.Empty);

        Assert.That(store.TryRollbackLoadoutReservation("prepared-raid"), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetLoadout(), Is.EquivalentTo(new[]
        {
            new StashItem(new LootId("bone"), 3),
            new StashItem(recovery, 1)
        }));
        Assert.That(store.GetPreparedWeapons().WeaponSlot1, Is.EqualTo(recovery));
    }

    [Test]
    public void Store_LoadoutReservationRollbackRestoresExactContentAndConfirmConsumesIt()
    {
        var files = new MemoryFileStore();
        var profile = new ProfileId("99999999999999999999999999999999");
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, _catalog), Is.True);
        var snapshot = repository.Snapshot.Clone();
        snapshot.Loadout.Add(new StashItem(new LootId("training_sword"), 1));
        snapshot.Loadout.Add(new StashItem(new LootId("bone"), 2));
        Assert.That(repository.TrySave(snapshot, out _), Is.True);
        var store = new LocalProfileStore(repository, profile, _catalog);
        Assert.That(
            store.TryAssignPreparedWeapon(WeaponSlot.Slot1, new LootId("training_sword")),
            Is.EqualTo(StashOperationResult.Success));

        Assert.That(store.TryCreateLoadoutReservation("reservation-1", out PendingLoadoutReservation reservation), Is.EqualTo(StashOperationResult.Success));
        Assert.That(reservation.Items, Has.Count.EqualTo(2));
        Assert.That(reservation.PreparedWeapons.WeaponSlot1, Is.EqualTo(new LootId("training_sword")));
        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.TryRollbackLoadoutReservation("reservation-1"), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetLoadout(), Is.EquivalentTo(new[]
        {
            new StashItem(new LootId("training_sword"), 1),
            new StashItem(new LootId("bone"), 2)
        }));
        Assert.That(store.GetPreparedWeapons().WeaponSlot1, Is.EqualTo(new LootId("training_sword")));

        Assert.That(store.TryCreateLoadoutReservation("reservation-2", out _), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryConfirmLoadoutReservation("reservation-2"), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.PendingReservation, Is.Null);
        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.TryRollbackLoadoutReservation("reservation-2"), Is.EqualTo(StashOperationResult.InvalidInventory));

        Assert.That(
            store.TryImportItems(new[] { new StashItem(new LootId("training_sword"), 1) }),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(
            store.TryAssignPreparedWeapon(WeaponSlot.Slot1, new LootId("training_sword")),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryCreateLoadoutReservation("reservation-3", out _), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.PendingReservation.ReservationId, Is.EqualTo("reservation-3"));
        Assert.That(store.PendingReservation.ReservationId, Is.Not.EqualTo("reservation-2"));
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
