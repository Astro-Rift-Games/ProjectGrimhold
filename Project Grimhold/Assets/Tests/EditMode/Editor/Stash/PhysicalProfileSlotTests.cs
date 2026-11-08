#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PhysicalProfileSlotTests
{
    private static readonly LootId Bone = new("bone");
    private LootDefinitionCatalog Catalog => AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
        "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");

    [Test]
    public void Receive_UsesLowestMatchingSlot_AndRemovalLeavesOtherSlotsUntouched()
    {
        var items = new List<StashItem> { new(Bone, 3, 7), new(Bone, 2, 1) };
        Assert.That(ProfileInventoryRules.TryMerge(items, new[] { new StashItem(Bone, 4) }, 10), Is.True);
        Assert.That(items.Single(x => x.Slot == 1).Amount, Is.EqualTo(6));
        Assert.That(ProfileInventoryRules.FindAmount(items, Bone), Is.EqualTo(9));
        Assert.That(ProfileInventoryRules.TryRemoveAt(items, 1, Bone, 6), Is.True);
        Assert.That(items, Is.EqualTo(new[] { new StashItem(Bone, 3, 7) }));
    }

    [Test]
    public void Receive_FillsLowestHole_RejectsFullAndOverflowGrowth_Atomically()
    {
        var other = new LootId("other");
        var items = new List<StashItem> { new(Bone, 2, 1) };
        Assert.That(ProfileInventoryRules.TryMerge(items, new[] { new StashItem(other, 1) }, 2), Is.True);
        Assert.That(items.Single(x => x.LootId == other).Slot, Is.Zero);
        var before = items.ToArray();
        Assert.That(ProfileInventoryRules.TryMerge(items, new[] { new StashItem(new LootId("third"), 1) }, 2), Is.False);
        Assert.That(items, Is.EqualTo(before));
        items = new List<StashItem> { new(Bone, 2, 2) };
        before = items.ToArray();
        Assert.That(ProfileInventoryRules.TryMerge(items, new[] { new StashItem(Bone, 1) }, 2), Is.False);
        Assert.That(items, Is.EqualTo(before));
        Assert.That(ProfileInventoryRules.TryRemoveAt(items, 2, Bone, 1), Is.True);
        Assert.That(items.Single(), Is.EqualTo(new StashItem(Bone, 1, 2)));
    }

    [Test]
    public void Consumption_DrainsStacksInAscendingSlotOrder()
    {
        var items = new List<StashItem> { new(Bone, 3, 8), new(Bone, 2, 1) };
        Assert.That(ProfileInventoryRules.TryRemove(items, Bone, 4), Is.True);
        Assert.That(items, Is.EqualTo(new[] { new StashItem(Bone, 1, 8) }));
        var before = items.ToArray();
        Assert.That(ProfileInventoryRules.TryRemove(items, Bone, 2), Is.False);
        Assert.That(items, Is.EqualTo(before));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Store_ReceivesUnplacedInputs_UsingLowestMatchingSlotThenLowestHole(bool toStash)
    {
        var id = new ProfileId("49494949494949494949494949494949");
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(id, Catalog), Is.True);
        var initial = repository.Snapshot.Clone();
        var destination = toStash ? initial.Stash : initial.Loadout;
        destination.AddRange(new[] { new StashItem(Bone, 2, 1), new StashItem(Bone, 3, 7) });
        Assert.That(repository.TrySave(initial, out _), Is.True);
        var store = new LocalProfileStore(repository, id, Catalog);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;
        var sword = new LootId("rapier");
        var incoming = new[] { new StashItem(Bone, 4), new StashItem(sword, 1) };

        Assert.That(toStash ? store.TrySecureLoot(incoming) : store.TryImportItems(incoming),
            Is.EqualTo(StashOperationResult.Success));

        Assert.That(toStash ? store.GetStash() : store.GetLoadout(), Is.EqualTo(new[]
        {
            new StashItem(sword, 1, 0), new StashItem(Bone, 6, 1), new StashItem(Bone, 3, 7)
        }));
        Assert.That(toStash ? store.GetLoadout() : store.GetStash(), Is.Empty);
        Assert.That(store.GetPreparedEquipment().HasAnyEquipment, Is.False);
        Assert.That(store.PendingReservation, Is.Null);
        Assert.That(store.GetCurrency(), Is.EqualTo(initial.Currency));
        Assert.That(incoming[0].Slot, Is.EqualTo(-1));
        Assert.That(incoming[1].Slot, Is.EqualTo(-1));
        Assert.That(commits, Is.EqualTo(1));
    }

    [Test]
    public void Codec_V10_RoundTripsDuplicateIdsHolesAndBackendVersion_AndRejectsDuplicateSlots()
    {
        var id = new ProfileId("49494949494949494949494949494949");
        var snapshot = new LocalProfileSnapshot { ProfileId = id, InventorySchemaVersion = 2 };
        snapshot.Stash.AddRange(new[] { new StashItem(Bone, 3, 7), new StashItem(Bone, 2, 1) });
        snapshot.Loadout.AddRange(new[] { new StashItem(Bone, 4, 2), new StashItem(Bone, 5, 9) });
        snapshot.PendingReservation = new PendingLoadoutReservation("reserved", snapshot.Loadout,
            new PreparedEquipmentLoadout(new LootId("rapier"), default));
        string json = LocalProfileSaveCodec.Encode(snapshot);
        Assert.That(LocalProfileSaveCodec.TryDecode(json, id, Catalog, out var decoded, out _, out var error), Is.True, error);
        Assert.That(decoded.Stash, Is.EqualTo(snapshot.Stash));
        Assert.That(decoded.Loadout, Is.EqualTo(snapshot.Loadout));
        Assert.That(decoded.PendingReservation.Items, Is.EqualTo(snapshot.PendingReservation.Items));
        Assert.That(decoded.InventorySchemaVersion, Is.EqualTo(2));
        snapshot.Stash.Add(new StashItem(Bone, 4, 1));
        Assert.That(LocalProfileSaveCodec.TryDecode(LocalProfileSaveCodec.Encode(snapshot), id, Catalog,
            out _, out _, out _), Is.False);
    }

    [Test]
    public void Codec_V9_AssignsListIndicesWithoutChangingQuantities_AndFutureVersionFailsClosed()
    {
        var id = new ProfileId("49494949494949494949494949494949");
        var snapshot = new LocalProfileSnapshot { ProfileId = id };
        snapshot.Stash.Add(new StashItem(Bone, 3, 7));
        snapshot.Stash.Add(new StashItem(new LootId("healthpotion"), 4, 11));
        snapshot.Loadout.Add(new StashItem(Bone, 5, 9));
        string json = LocalProfileSaveCodec.Encode(snapshot).Replace($"\"schemaVersion\": {LocalProfileSnapshot.CurrentSchemaVersion}", "\"schemaVersion\": 9");
        Assert.That(LocalProfileSaveCodec.TryDecode(json, id, Catalog, out var decoded, out _, out var error), Is.True, error);
        Assert.That(decoded.Stash, Is.EqualTo(new[] { new StashItem(Bone, 3, 0), new StashItem(new LootId("healthpotion"), 4, 1) }));
        Assert.That(decoded.Loadout.Single(), Is.EqualTo(new StashItem(Bone, 5, 0)));
        Assert.That(LocalProfileSaveCodec.TryDecode(LocalProfileSaveCodec.Encode(decoded), id, Catalog,
            out var roundTrip, out _, out error), Is.True, error);
        Assert.That(roundTrip.Stash, Is.EqualTo(decoded.Stash));
        Assert.That(decoded.InventorySchemaVersion, Is.EqualTo(1));
        json = json.Replace("\"schemaVersion\": 9", $"\"schemaVersion\": {LocalProfileSnapshot.CurrentSchemaVersion + 1}");
        Assert.That(LocalProfileSaveCodec.TryDecode(json, id, Catalog, out _, out var status, out _), Is.False);
        Assert.That(status, Is.EqualTo(LocalProfilePersistenceStatus.UnsupportedVersion));
    }

    [Test]
    public async Task Coordinator_LegacyBackend_AllMutationsUnavailable_WithoutRemoteCalls()
    {
        var id = new ProfileId("49494949494949494949494949494949");
        var repository = new InMemoryLocalProfileRepository();
        repository.Initialize(id, Catalog);
        var store = new LocalProfileStore(repository, id, Catalog);
        var go = new GameObject("slot services");
        try
        {
            var stash = go.AddComponent<InMemoryPlayerStashService>(); stash.Initialize(store);
            var loadout = go.AddComponent<InMemoryPlayerLoadoutService>(); loadout.Initialize(store);
            var remote = new CountingRemote();
            var coordinator = new StashTransferCoordinator(id, stash, loadout, remote, null);
            Assert.That((await coordinator.TransferAsync(Bone, true, LootTransferQuantityMode.SingleUnit)).Status,
                Is.EqualTo(StashTransferStatus.Unavailable));
            Assert.That((await coordinator.SortAsync(InventoryList.Stash)).Status, Is.EqualTo(StashTransferStatus.Unavailable));
            Assert.That((await coordinator.TakeAllAsync()).Status, Is.EqualTo(StashTransferStatus.Unavailable));
            Assert.That((await coordinator.EquipAsync(Bone, EquipmentSlot.WeaponSetAMainHand, true)).Status,
                Is.EqualTo(StashTransferStatus.Unavailable));
            Assert.That((await coordinator.UnequipAsync(EquipmentSlot.WeaponSetAMainHand)).Status,
                Is.EqualTo(StashTransferStatus.Unavailable));
            Assert.That(remote.Calls, Is.Zero);
            Assert.That(store.GetStash(), Is.Empty);
            Assert.That(store.GetLoadout(), Is.Empty);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void ReservationRollback_RestoresExactSlots_WithoutMergingDuplicateIds()
    {
        var id = new ProfileId("49494949494949494949494949494949");
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(id, Catalog), Is.True);
        var initial = repository.Snapshot.Clone();
        initial.InventorySchemaVersion = 2;
        initial.Loadout.AddRange(new[] { new StashItem(Bone, 3, 1), new StashItem(Bone, 2, 7) });
        initial.PreparedEquipment = new PreparedEquipmentLoadout(new LootId("rapier"), default);
        Assert.That(repository.TrySave(initial, out _), Is.True);
        var store = new LocalProfileStore(repository, id, Catalog);
        Assert.That(store.TryCreateLoadoutReservation("reserved", out var reservation), Is.EqualTo(StashOperationResult.Success));
        Assert.That(reservation.Items, Is.EqualTo(initial.Loadout));
        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.TryRollbackLoadoutReservation("reserved"), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetLoadout(), Is.EqualTo(initial.Loadout));
        Assert.That(store.GetPreparedEquipment(), Is.EqualTo(initial.PreparedEquipment));
        Assert.That(store.PendingReservation, Is.Null);
    }

    [Test]
    public void RollbackCollision_UsesLowestFreeThenOverflow_NeverMergesOrLosesUnits()
    {
        var destination = new List<StashItem> { new(Bone, 9, 0), new(Bone, 9, 1) };
        ProfileInventoryRules.RestoreSlots(destination, new[] { new StashItem(Bone, 2, 0), new StashItem(Bone, 3, 1) });
        Assert.That(destination, Is.EqualTo(new[]
        {
            new StashItem(Bone, 9, 0), new StashItem(Bone, 9, 1), new StashItem(Bone, 2, 2), new StashItem(Bone, 3, 3)
        }));
    }

    [TestCase(1)]
    [TestCase(30)]
    public void RaidAdmissionCapacity_ExtractionThenReservation_KeepsEquipmentOutsideBackpack(int backpackUnits)
    {
        LocalProfileStore store = CreateCapacityStore(out var repository, out var files);
        var profile = repository.Snapshot.ProfileId;
        var weapon = new LootId("arming_sword");
        Assert.That(Catalog.TryGet(weapon.Value, out var definition), Is.True);
        var modifiers = EquipmentInstance.PlainCache(definition).Modifiers;
        var items = Enumerable.Range(0, backpackUnits)
            .Select(slot => new StashItem(weapon, 1, slot,
                new EquipmentInstance((slot + 1).ToString("x24"), modifiers))).ToArray();
        var equippedInstance = new EquipmentInstance("ffffffffffffffffffffffff", modifiers);
        var equipment = default(PreparedEquipmentLoadout)
            .With(EquipmentSlot.WeaponSetAMainHand, weapon, equippedInstance);
        var receipt = new ExtractionReceipt("capacity-raid", profile, 1);
        int events = 0;
        store.ProfileCommitted += _ => events++;
        Assert.That(store.TryCommitExtraction(receipt, items, equipment, 0, 1, 0),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetLoadout(), Is.EqualTo(items));
        int writes = files.Writes;
        // RED must report the reservation rejection, not fail merely because it logged it.
        LogAssert.ignoreFailingMessages = true;
        try
        {
            Assert.That(store.TryCreateLoadoutReservation("capacity-next", out var reservation),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(reservation.Items, Is.EqualTo(items));
            Assert.That(reservation.PreparedEquipment, Is.EqualTo(equipment));
            Assert.That(reservation.PreparedEquipment.GetInstance(EquipmentSlot.WeaponSetAMainHand),
                Is.EqualTo(equippedInstance));
        }
        finally { LogAssert.ignoreFailingMessages = false; }
        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.GetPreparedEquipment().HasAnyEquipment, Is.False);
        Assert.That(store.GetStash(), Is.Empty);
        Assert.That(store.GetCurrency(), Is.EqualTo(10));
        Assert.That(store.PendingExtractionCommit.Receipt, Is.EqualTo(receipt));
        Assert.That(events, Is.EqualTo(2));
        Assert.That(files.Writes, Is.EqualTo(writes + 1));
        Assert.That(store.TryCreateLoadoutReservation("capacity-next", out var retry),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(retry.Items, Is.EqualTo(items));
        Assert.That(retry.PreparedEquipment, Is.EqualTo(equipment));
        Assert.That(files.Writes, Is.EqualTo(writes + 1));
        Assert.That(events, Is.EqualTo(2));
    }

    [Test]
    public void RaidAdmissionCapacity_TrueBackpackStackOverflow_LeavesProfileAndCountersUnchanged()
    {
        LocalProfileStore store = CreateCapacityStore(out var repository, out var files);
        Assert.That(Catalog.TryGet(Bone.Value, out var definition), Is.True);
        var receipt = new ExtractionReceipt("capacity-overflow", repository.Snapshot.ProfileId, 1);
        var items = new[] { new StashItem(Bone, definition.StackMax * 31) };
        Assert.That(store.TryCommitExtraction(receipt, items, default, 0, 1, 0),
            Is.EqualTo(StashOperationResult.Success));
        string before = LocalProfileSaveCodec.Encode(repository.Snapshot);
        int writes = files.Writes, events = 0;
        store.ProfileCommitted += _ => events++;
        LogAssert.Expect(LogType.Error,
            "[LocalProfileStore] TryCreateLoadoutReservation failed. Physical slots required (31) exceeds MaxLoadoutSlots (30).");
        Assert.That(store.TryCreateLoadoutReservation("capacity-rejected", out var reservation),
            Is.EqualTo(StashOperationResult.InventoryFull));
        Assert.That(reservation, Is.Null);
        Assert.That(store.PendingReservation, Is.Null);
        Assert.That(LocalProfileSaveCodec.Encode(repository.Snapshot), Is.EqualTo(before));
        Assert.That(store.GetStash(), Is.Empty);
        Assert.That(files.Writes, Is.EqualTo(writes));
        Assert.That(events, Is.Zero);
    }

    private LocalProfileStore CreateCapacityStore(out LocalProfileRepository repository,
        out CapacityProfileFiles files)
    {
        var profile = new ProfileId("49494949494949494949494949494949");
        files = new CapacityProfileFiles();
        repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(profile, Catalog), Is.True, repository.LastError);
        var store = new LocalProfileStore(repository, profile, Catalog);
        Assert.That(store.ReconcileRemoteState(new InventoryData
        {
            inventorySchemaVersion = 3, revision = 7, currency = 10,
            stash = System.Array.Empty<InventoryItemData>(),
            loadout = System.Array.Empty<InventoryItemData>(), preparedEquipment = new PreparedEquipmentData()
        }, null, Catalog, null), Is.EqualTo(StashOperationResult.Success));
        return store;
    }

    private sealed class CapacityProfileFiles : ILocalProfileFileStore
    {
        private readonly Dictionary<string, string> _contents = new();
        public int Writes { get; private set; }
        public bool Exists(string path) => _contents.ContainsKey(path);
        public bool TryRead(string path, out string contents, out string error)
        {
            error = _contents.TryGetValue(path, out contents) ? null : "Missing";
            return error == null;
        }
        public bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath,
            string contents, out string error)
        {
            _contents[mainPath] = contents;
            Writes++;
            error = null;
            return true;
        }
        public bool TryRestoreMainFromBackup(string mainPath, string backupPath, out string error)
        {
            error = "Not used";
            return false;
        }
    }

    [Test]
    public void Extraction_ReceivesAggregateDuplicatesIntoEmptyLoadout_AndPersistsAggregateRetry()
    {
        var id = new ProfileId("49494949494949494949494949494949");
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(id, Catalog), Is.True);
        var store = new LocalProfileStore(repository, id, Catalog);
        var items = new[] { new StashItem(Bone, 3), new StashItem(Bone, 2) };
        Assert.That(store.TryCommitExtraction(new ExtractionReceipt("raid", id, 1), items,
            default, 0, 1, 0), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Bone, 5, 0) }));
        Assert.That(store.PendingExtractionCommit.Items, Is.EqualTo(items));
        var before = LocalProfileSaveCodec.Encode(repository.Snapshot);
        Assert.That(store.TryCommitExtraction(new ExtractionReceipt("other", id, 2), items,
            default, 0, 1, 0), Is.EqualTo(StashOperationResult.PersistenceFailed));
        Assert.That(LocalProfileSaveCodec.Encode(repository.Snapshot), Is.EqualTo(before));
    }

    [Test]
    public void Hydration_V2PreservesDuplicatesAndSlots_V1UsesOriginalListIndices()
    {
        var id = new ProfileId("49494949494949494949494949494949");
        var snapshot = new LocalProfileSnapshot { ProfileId = id };
        var data = new InventoryData { inventorySchemaVersion = 2, stash = new[]
        {
            new InventoryItemData { slot = 7, lootId = "bone", amount = 3 },
            new InventoryItemData { slot = 1, lootId = "bone", amount = 2 }
        } };
        Assert.That(ApplicationStashServiceBootstrapper.HydrateSnapshot(id, snapshot, data, null, Catalog, null), Is.True);
        Assert.That(snapshot.Stash, Is.EqualTo(new[] { new StashItem(Bone, 2, 1), new StashItem(Bone, 3, 7) }));
        Assert.That(snapshot.InventorySchemaVersion, Is.EqualTo(2));
        data.inventorySchemaVersion = 1;
        Assert.That(ApplicationStashServiceBootstrapper.HydrateSnapshot(id, snapshot, data, null, Catalog, null), Is.True);
        Assert.That(snapshot.Stash, Is.EqualTo(new[] { new StashItem(Bone, 3, 0), new StashItem(Bone, 2, 1) }));
        Assert.That(snapshot.InventorySchemaVersion, Is.EqualTo(1));
    }

    [Test]
    public void SlotMove_EmptyMergeAndSwap_ConserveUnits_AndOverflowCannotReceive()
    {
        var other = new LootId("other");
        var source = new List<StashItem> { new(Bone, 3, 7) };
        var target = new List<StashItem>();
        Assert.That(ProfileInventoryRules.TryMove(source, 7, target, 1, Bone, 10, 3), Is.True);
        Assert.That(source, Is.Empty);
        Assert.That(target, Is.EqualTo(new[] { new StashItem(Bone, 3, 1) }));
        source.Add(new StashItem(Bone, 2, 0));
        Assert.That(ProfileInventoryRules.TryMove(source, 0, target, 1, Bone, 10, 3), Is.True);
        Assert.That(target.Single().Amount, Is.EqualTo(5));
        source.Add(new StashItem(other, 4, 0));
        Assert.That(ProfileInventoryRules.TryMove(source, 0, target, 1, other, 10, 3), Is.True);
        Assert.That(source.Single(), Is.EqualTo(new StashItem(Bone, 5, 0)));
        Assert.That(target.Single(), Is.EqualTo(new StashItem(other, 4, 1)));
        var before = target.ToArray();
        Assert.That(ProfileInventoryRules.TryMove(source, 0, target, 3, Bone, 10, 3), Is.False);
        Assert.That(target, Is.EqualTo(before));
        Assert.That(source.Single(), Is.EqualTo(new StashItem(Bone, 5, 0)));
    }

    [Test]
    public void ExplicitOrder_CompactsSlotsWithoutMerging_AndRejectsNonPermutationsWithoutCommit()
    {
        var id = new ProfileId("49494949494949494949494949494949");
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(id, Catalog), Is.True);
        var initial = repository.Snapshot.Clone();
        initial.Stash.AddRange(new[] { new StashItem(Bone, 2, 1), new StashItem(Bone, 3, 7) });
        repository.TrySave(initial, out _);
        var store = new LocalProfileStore(repository, id, Catalog);
        int commits = 0; store.ProfileCommitted += _ => commits++;
        Assert.That(store.TryOrderSlots(InventoryList.Stash, new[] { 7, 7 }), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(store.GetStash(), Is.EqualTo(initial.Stash));
        Assert.That(commits, Is.Zero);
        Assert.That(store.TryOrderSlots(InventoryList.Stash, new[] { 7, 1 }), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 3, 0), new StashItem(Bone, 2, 1) }));
        Assert.That(commits, Is.EqualTo(1));
    }

    [Test]
    public async Task Coordinator_AddressedTransferUsesExactStack_RemoteFirst_ThenLeavesHole()
    {
        var id = new ProfileId("49494949494949494949494949494949");
        var repository = new InMemoryLocalProfileRepository();
        repository.Initialize(id, Catalog);
        var initial = repository.Snapshot.Clone();
        initial.InventorySchemaVersion = 2;
        initial.Stash.AddRange(new[] { new StashItem(Bone, 3, 1), new StashItem(Bone, 2, 7) });
        repository.TrySave(initial, out _);
        var store = new LocalProfileStore(repository, id, Catalog);
        int commits = 0; store.ProfileCommitted += _ => commits++;
        var go = new GameObject("slot services");
        try
        {
            var stash = go.AddComponent<InMemoryPlayerStashService>(); stash.Initialize(store);
            var loadout = go.AddComponent<InMemoryPlayerLoadoutService>(); loadout.Initialize(store);
            var remote = new SlotRemote();
            remote.OnTransfer = () => Assert.That(store.GetStash(), Is.EqualTo(initial.Stash));
            var coordinator = new StashTransferCoordinator(id, stash, loadout, remote, null);
            var result = await coordinator.TransferAsync(7, Bone, true, LootTransferQuantityMode.FullStack);
            Assert.That(result.Status, Is.EqualTo(StashTransferStatus.Success));
            Assert.That(remote.Slot, Is.EqualTo(7));
            Assert.That(remote.Amount, Is.EqualTo(2));
            Assert.That(store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 3, 1) }));
            Assert.That(store.GetLoadout(), Is.EqualTo(new[] { new StashItem(Bone, 2, 0) }));
            Assert.That(commits, Is.EqualTo(1));
            result = await coordinator.TransferAsync(7, Bone, true, LootTransferQuantityMode.FullStack);
            Assert.That(result.Status, Is.EqualTo(StashTransferStatus.Rejected));
            Assert.That(remote.Calls, Is.EqualTo(1));
            Assert.That(commits, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void StoreConsumption_DrainsPhysicalStacksInOrder_AndFailurePublishesNothing()
    {
        var id = new ProfileId("49494949494949494949494949494949");
        var repository = new InMemoryLocalProfileRepository(); repository.Initialize(id, Catalog);
        var initial = repository.Snapshot.Clone();
        initial.Stash.AddRange(new[] { new StashItem(Bone, 3, 1), new StashItem(Bone, 2, 7) });
        repository.TrySave(initial, out _);
        var store = new LocalProfileStore(repository, id, Catalog);
        int commits = 0; store.ProfileCommitted += _ => commits++;
        Assert.That(store.TryConsumeLoot(Bone, 4), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 1, 7) }));
        Assert.That(commits, Is.EqualTo(1));
        Assert.That(store.TryConsumeLoot(Bone, 2), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(store.GetStash(), Is.EqualTo(new[] { new StashItem(Bone, 1, 7) }));
        Assert.That(commits, Is.EqualTo(1));
    }

    private sealed class SlotRemote : IRemoteInventoryService, ISlotRemoteInventoryService
    {
        public Task<(bool success, BackendError error)> SplitSlotAsync(InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId lootId) =>
            Task.FromResult((true, default(BackendError)));
        public System.Action OnTransfer;
        public int Slot, Amount, Calls;
        public Task<(bool success, BackendError error)> TransferAsync(int slot, LootId id, int amount, bool fromStash)
        {
            Slot = slot; Amount = amount; Calls++; OnTransfer?.Invoke();
            return Task.FromResult((true, default(BackendError)));
        }
        public Task<(bool success, BackendError error)> OrderSlotsAsync(InventoryList list, IReadOnlyList<int> slots) =>
            Task.FromResult((true, default(BackendError)));
        public Task<(bool success, BackendError error)> MoveSlotsAsync(InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id) =>
            Task.FromResult((true, default(BackendError)));
        public Task<(bool success, BackendError error)> OrderAsync(InventoryList list, IReadOnlyList<LootId> ids) => throw new System.InvalidOperationException();
        public Task<(bool success, BackendError error)> MoveToLoadoutAsync(LootId id, int amount) => throw new System.InvalidOperationException();
        public Task<(bool success, BackendError error)> MoveToStashAsync(LootId id, int amount) => throw new System.InvalidOperationException();
        public Task<(bool success, BackendError error)> UpdatePreparedEquipmentAsync(PreparedEquipmentLoadout equipment) =>
            Task.FromResult((true, default(BackendError)));
    }

    private sealed class CountingRemote : IRemoteInventoryService
    {
        public int Calls;
        private Task<(bool, BackendError)> Call() { Calls++; return Task.FromResult((true, default(BackendError))); }
        public Task<(bool success, BackendError error)> OrderAsync(InventoryList list, IReadOnlyList<LootId> ids) => Call();
        public Task<(bool success, BackendError error)> MoveToLoadoutAsync(LootId id, int amount) => Call();
        public Task<(bool success, BackendError error)> MoveToStashAsync(LootId id, int amount) => Call();
        public Task<(bool success, BackendError error)> UpdatePreparedEquipmentAsync(PreparedEquipmentLoadout equipment) => Call();
    }
}
#endif
