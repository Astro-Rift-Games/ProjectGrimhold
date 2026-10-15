#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode.Stash
{
    /// <summary>
    /// Covers hydration from backend data through the productive file repository: backend data
    /// over the Loadout capacity is accepted and stays persistable, item lists are normalized
    /// before they reach the save codec, and an over-capacity Loadout only allows operations
    /// that do not grow the excess.
    /// </summary>
    public sealed class StashHydrationCapacityTests
    {
        private const string CatalogPath =
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";
        private const string ProfileValue = "49494949494949494949494949494949";

        private LootDefinitionCatalog _catalog;
        private ProfileId _profile;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null);
            _profile = new ProfileId(ProfileValue);
        }

        [Test]
        public void ReconcileRemoteState_LoadoutOverCapacity_IsAcceptedAndSurvivesReload()
        {
            var files = new MemoryFileStore();
            LocalProfileStore store = CreateStore(files, out _);
            LootId[] ids = DistinctLootIds(LocalProfileSnapshot.MaxLoadoutSlots + 1);

            StashOperationResult result = store.ReconcileRemoteState(Inventory(ids, Array.Empty<LootId>()), null, _catalog, null);

            Assert.That(result, Is.EqualTo(StashOperationResult.Success));
            Assert.That(store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots + 1));
            var reloaded = new LocalProfileRepository(files, ".");
            Assert.That(reloaded.Initialize(_profile, _catalog), Is.True, reloaded.LastError);
            Assert.That(reloaded.Snapshot.Loadout, Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots + 1));
        }

        [Test]
        public void OverCapacityLoadout_TransferToStash_ReducesTheExcessAndCommits()
        {
            LocalProfileStore store = CreateOverCapacityStore(out LootId[] ids);

            Assert.That(store.TryTransferToStash(ids[0], 1), Is.EqualTo(StashOperationResult.Success));
            Assert.That(store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
            Assert.That(store.GetStash().Single().LootId, Is.EqualTo(ids[0]));
        }

        [Test]
        public void OverCapacityLoadout_TransferNeedingANewSlot_IsInventoryFull()
        {
            LootId[] ids = DistinctLootIds(LocalProfileSnapshot.MaxLoadoutSlots + 2);
            LootId[] loadout = ids.Take(LocalProfileSnapshot.MaxLoadoutSlots + 1).ToArray();
            LootId extra = ids[LocalProfileSnapshot.MaxLoadoutSlots + 1];
            LocalProfileStore store = CreateStore(new MemoryFileStore(), out _);
            Assert.That(
                store.ReconcileRemoteState(Inventory(loadout, new[] { extra }), null, _catalog, null),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.TryTransferToLoadout(extra, 1), Is.EqualTo(StashOperationResult.InventoryFull));
            Assert.That(store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots + 1));
        }

        [Test]
        public void OverCapacityLoadout_CannotBeReservedForARaid()
        {
            LocalProfileStore store = CreateOverCapacityStore(out _);

            StashOperationResult result = store.TryCreateLoadoutReservation("over-capacity", out PendingLoadoutReservation reservation);

            Assert.That(result, Is.EqualTo(StashOperationResult.InventoryFull));
            Assert.That(reservation, Is.Null);
            Assert.That(store.PendingReservation, Is.Null);
            Assert.That(store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots + 1));
        }

        [Test]
        public void ReconcileRemoteState_DuplicateAndNonPositiveItems_AreNormalizedAndLaterCommitsSucceed()
        {
            LootId[] ids = DistinctLootIds(3);
            LocalProfileStore store = CreateStore(new MemoryFileStore(), out _);
            var data = new InventoryData
            {
                revision = 3,
                stash = new[]
                {
                    Item(ids[0], 2),
                    Item(ids[0], 3),
                    Item(ids[1], 0),
                    Item(ids[2], -4)
                },
                loadout = Array.Empty<InventoryItemData>(),
                preparedEquipment = new PreparedEquipmentData()
            };

            Assert.That(store.ReconcileRemoteState(data, null, _catalog, null), Is.EqualTo(StashOperationResult.Success));
            CollectionAssert.AreEqual(new[] { new StashItem(ids[0], 5) }, store.GetStash());
            Assert.That(store.TryTransferToLoadout(ids[0], 1), Is.EqualTo(StashOperationResult.Success));
        }

        [Test]
        public void HydrateSnapshot_AmountOverflowOnMerge_FailsWithoutChangingTheSnapshot()
        {
            LootId[] ids = DistinctLootIds(2);
            var snapshot = new LocalProfileSnapshot { ProfileId = _profile, Currency = 7 };
            snapshot.Stash.Add(new StashItem(ids[1], 4));
            var data = new InventoryData
            {
                revision = 9,
                currency = 100,
                stash = new[] { Item(ids[0], int.MaxValue), Item(ids[0], 1) },
                loadout = Array.Empty<InventoryItemData>(),
                preparedEquipment = new PreparedEquipmentData()
            };

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("stash amount overflow"));
            bool result = ApplicationStashServiceBootstrapper.HydrateSnapshot(_profile, snapshot, data, null, _catalog, null);

            Assert.That(result, Is.False);
            Assert.That(snapshot.Currency, Is.EqualTo(7));
            Assert.That(snapshot.RemoteRevision, Is.EqualTo(0));
            CollectionAssert.AreEqual(new[] { new StashItem(ids[1], 4) }, snapshot.Stash);
        }

        private LocalProfileStore CreateOverCapacityStore(out LootId[] ids)
        {
            ids = DistinctLootIds(LocalProfileSnapshot.MaxLoadoutSlots + 1);
            LocalProfileStore store = CreateStore(new MemoryFileStore(), out _);
            Assert.That(
                store.ReconcileRemoteState(Inventory(ids, Array.Empty<LootId>()), null, _catalog, null),
                Is.EqualTo(StashOperationResult.Success));
            return store;
        }

        private LocalProfileStore CreateStore(MemoryFileStore files, out LocalProfileRepository repository)
        {
            repository = new LocalProfileRepository(files, ".");
            Assert.That(repository.Initialize(_profile, _catalog), Is.True, repository.LastError);
            return new LocalProfileStore(repository, _profile, _catalog);
        }

        private static InventoryData Inventory(IReadOnlyList<LootId> loadout, IReadOnlyList<LootId> stash) =>
            new InventoryData
            {
                revision = 2,
                loadout = loadout.Select(id => Item(id, 1)).ToArray(),
                stash = stash.Select(id => Item(id, 1)).ToArray(),
                preparedEquipment = new PreparedEquipmentData()
            };

        private static InventoryItemData Item(LootId lootId, int amount) =>
            new InventoryItemData { lootId = lootId.Value, amount = amount };

        private LootId[] DistinctLootIds(int count)
        {
            var ids = new LootId[count];
            int found = 0;
            for (int index = 0; index < _catalog.DefinitionCount && found < count; index++)
            {
                if (_catalog.TryGetByIndex(index, out LootDefinition definition))
                {
                    ids[found++] = definition.LootId;
                }
            }

            Assert.That(found, Is.EqualTo(count), "The loot catalog needs enough distinct definitions.");
            return ids;
        }

        private sealed class MemoryFileStore : ILocalProfileFileStore
        {
            private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

            public bool Exists(string path) => _files.ContainsKey(path);

            public bool TryRead(string path, out string contents, out string error)
            {
                error = _files.TryGetValue(path, out contents) ? null : "Missing file";
                return error == null;
            }

            public bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath, string contents, out string error)
            {
                if (_files.TryGetValue(mainPath, out string previous)) _files[backupPath] = previous;
                _files[mainPath] = contents;
                error = null;
                return true;
            }

            public bool TryRestoreMainFromBackup(string mainPath, string backupPath, out string error)
            {
                if (!_files.TryGetValue(backupPath, out string backup))
                {
                    error = "Missing backup";
                    return false;
                }

                _files[mainPath] = backup;
                error = null;
                return true;
            }
        }
    }
}
#endif
