#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Stash
{
    /// <summary>
    /// Covers the dry-run evaluators: they report exactly what the matching transfer would return
    /// right now, without changing or publishing anything, so a presenter can reject an action
    /// before it reaches the backend.
    /// </summary>
    public sealed class StashTransferEvaluationTests
    {
        private const string CatalogPath =
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";
        private const string ProfileValue = "48484848484848484848484848484848";

        private LootDefinitionCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null);
        }

        [Test]
        public void EvaluateTransferToLoadout_FullLoadout_ReportsInventoryFullAndMatchesTheTransfer()
        {
            LootId[] ids = DistinctLootIds(LocalProfileSnapshot.MaxLoadoutSlots + 1);
            LocalProfileStore store = CreateStore(snapshot =>
            {
                foreach (LootId id in ids.Take(LocalProfileSnapshot.MaxLoadoutSlots))
                {
                    snapshot.Loadout.Add(new StashItem(id, 1));
                }

                snapshot.Stash.Add(new StashItem(ids[LocalProfileSnapshot.MaxLoadoutSlots], 1));
            });
            int commits = 0;
            store.ProfileCommitted += _ => commits++;
            LootId overflow = ids[LocalProfileSnapshot.MaxLoadoutSlots];

            StashOperationResult evaluation = store.EvaluateTransferToLoadout(overflow, 1);

            Assert.That(evaluation, Is.EqualTo(StashOperationResult.InventoryFull));
            Assert.That(commits, Is.Zero);
            Assert.That(store.GetStash(), Has.Count.EqualTo(1));
            Assert.That(store.GetLoadout(), Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
            Assert.That(store.TryTransferToLoadout(overflow, 1), Is.EqualTo(evaluation));
            Assert.That(commits, Is.Zero);
        }

        [Test]
        public void EvaluateTransferToLoadout_ValidTransfer_SucceedsWithoutMovingAnythingUntilTheTransferRuns()
        {
            var bone = new LootId("bone");
            LocalProfileStore store = CreateStore(snapshot => snapshot.Stash.Add(new StashItem(bone, 3)));
            int commits = 0;
            store.ProfileCommitted += _ => commits++;

            Assert.That(store.EvaluateTransferToLoadout(bone, 2), Is.EqualTo(StashOperationResult.Success));

            Assert.That(commits, Is.Zero);
            Assert.That(store.GetStash().Single().Amount, Is.EqualTo(3));
            Assert.That(store.GetLoadout(), Is.Empty);
            Assert.That(store.TryTransferToLoadout(bone, 2), Is.EqualTo(StashOperationResult.Success));
            Assert.That(store.GetLoadout().Single().Amount, Is.EqualTo(2));
        }

        [Test]
        public void EvaluateTransferAllToLoadout_ExceedingCapacity_ReportsInventoryFull()
        {
            LootId[] ids = DistinctLootIds(LocalProfileSnapshot.MaxLoadoutSlots + 1);
            LocalProfileStore store = CreateStore(snapshot =>
            {
                foreach (LootId id in ids)
                {
                    snapshot.Stash.Add(new StashItem(id, 1));
                }
            });

            Assert.That(
                store.EvaluateTransferAllToLoadout(),
                Is.EqualTo(StashOperationResult.InventoryFull));
            Assert.That(
                store.TryTransferAllToLoadout(),
                Is.EqualTo(StashOperationResult.InventoryFull));
            Assert.That(store.GetStash(), Has.Count.EqualTo(ids.Length));
            Assert.That(store.GetLoadout(), Is.Empty);
        }

        [Test]
        public void EvaluateTransferAll_WithNothingToMove_Succeeds()
        {
            LocalProfileStore store = CreateStore(null);

            Assert.That(store.EvaluateTransferAllToLoadout(), Is.EqualTo(StashOperationResult.Success));
            Assert.That(store.EvaluateTransferAllToStash(), Is.EqualTo(StashOperationResult.Success));
        }

        [Test]
        public void EvaluateTransferToStash_ItemNotInLoadout_IsInvalid()
        {
            LocalProfileStore store = CreateStore(null);

            Assert.That(
                store.EvaluateTransferToStash(new LootId("bone"), 1),
                Is.EqualTo(StashOperationResult.InvalidInventory));
        }

        [Test]
        public void Evaluators_WhileAReservationIsPending_ReportReservationPending()
        {
            var sword = new LootId("rapier");
            var bone = new LootId("bone");
            LocalProfileStore store = CreateStore(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(bone, 1));
                snapshot.Loadout.Add(new StashItem(sword, 1));
                snapshot.PreparedEquipment = new PreparedEquipmentLoadout(sword, default);
            });
            Assert.That(
                store.TryCreateLoadoutReservation("reservation-b", out _),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(
                store.EvaluateTransferToLoadout(bone, 1),
                Is.EqualTo(StashOperationResult.ReservationPending));
            Assert.That(
                store.EvaluateTransferToStash(bone, 1),
                Is.EqualTo(StashOperationResult.ReservationPending));
            Assert.That(
                store.EvaluateTransferAllToLoadout(),
                Is.EqualTo(StashOperationResult.ReservationPending));
            Assert.That(
                store.EvaluateTransferAllToStash(),
                Is.EqualTo(StashOperationResult.ReservationPending));
        }

        [Test]
        public void Evaluators_OnAnUnavailableProfile_ReportInvalidInventoryInsteadOfThrowing()
        {
            var store = new LocalProfileStore(
                new InMemoryLocalProfileRepository(),
                new ProfileId(ProfileValue),
                _catalog);
            var bone = new LootId("bone");

            Assert.That(store.EvaluateTransferToLoadout(bone, 1), Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(store.EvaluateTransferToStash(bone, 1), Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(store.EvaluateTransferAllToLoadout(), Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(store.EvaluateTransferAllToStash(), Is.EqualTo(StashOperationResult.InvalidInventory));
        }

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

        private LocalProfileStore CreateStore(Action<LocalProfileSnapshot> configure)
        {
            var profile = new ProfileId(ProfileValue);
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profile, _catalog), Is.True);
            LocalProfileSnapshot snapshot = repository.Snapshot.Clone();
            configure?.Invoke(snapshot);
            Assert.That(repository.TrySave(snapshot, out string error), Is.True, error);
            return new LocalProfileStore(repository, profile, _catalog);
        }
    }
}
#endif
