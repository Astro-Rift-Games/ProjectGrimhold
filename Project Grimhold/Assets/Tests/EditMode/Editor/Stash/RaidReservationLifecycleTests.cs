#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Stash
{
    public sealed class RaidReservationLifecycleTests
    {
        private LootDefinitionCatalog _catalog;
        private readonly ProfileId _profile = new("47474747474747474747474747474747");

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(_catalog, Is.Not.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void HydratedOrphan_AdmittedLosesItems_OtherwiseRestores(bool admitted)
        {
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(_profile, _catalog), Is.True);
            LocalProfileSnapshot snapshot = repository.Snapshot.Clone();
            var inventory = new InventoryData
            {
                revision = 7,
                stash = new[] { new InventoryItemData { lootId = "bone", amount = 3 } },
                loadout = Array.Empty<InventoryItemData>(),
                pendingReservation = new PendingReservationData
                {
                    reservationId = "reservation-a",
                    admittedAt = admitted ? "2026-10-29T12:00:00.000Z" : null,
                    items = new[] { new InventoryItemData { lootId = "bone", amount = 2 } },
                    preparedEquipment = new PreparedEquipmentData { weaponSlot1 = "rapier" }
                }
            };
            Assert.That(ApplicationStashServiceBootstrapper.HydrateSnapshot(
                _profile, snapshot, inventory, null, _catalog, null), Is.True);
            Assert.That(repository.TrySave(snapshot, out string error), Is.True, error);
            var store = new LocalProfileStore(repository, _profile, _catalog);
            int commits = 0;
            store.ProfileCommitted += _ => commits++;

            Assert.That(ApplicationStashServiceBootstrapper.TryResolveOrphanedReservation(
                store, !string.IsNullOrEmpty(inventory.pendingReservation.admittedAt)),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.PendingReservation, Is.Null);
            Assert.That(store.PendingExtractionCommit, Is.Null);
            Assert.That(store.GetStash(), Is.EqualTo(new[] { new StashItem(new LootId("bone"), 3) }));
            Assert.That(store.GetLoadout(), Is.EqualTo(admitted
                ? Array.Empty<StashItem>() : new[] { new StashItem(new LootId("bone"), 2) }));
            Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand,
                Is.EqualTo(admitted ? default(LootId) : new LootId("rapier")));
            Assert.That(store.RemoteRevision, Is.EqualTo(7));
            Assert.That(commits, Is.EqualTo(1));
            Assert.That(LocalProfileSaveCodec.TryDecode(LocalProfileSaveCodec.Encode(repository.Snapshot),
                _profile, _catalog, out _, out _, out error), Is.True, error);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OrphanRecovery_PendingExtractionIsNotDiscardedOrRolledBack(bool admitted)
        {
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(_profile, _catalog), Is.True);
            var store = new LocalProfileStore(repository, _profile, _catalog);
            var receipt = new ExtractionReceipt("raid-a", _profile, 1);
            Assert.That(store.TryCommitExtraction(receipt, Array.Empty<StashItem>(), default, 0, 1, 0),
                Is.EqualTo(StashOperationResult.Success));
            LocalProfileSnapshot hydrated = repository.Snapshot.Clone();
            hydrated.PendingReservation = new PendingLoadoutReservation("reservation-a",
                new[] { new StashItem(new LootId("bone"), 2) },
                new PreparedEquipmentLoadout(new LootId("rapier"), default));
            Assert.That(repository.TrySave(hydrated, out string error), Is.True, error);
            string before = LocalProfileSaveCodec.Encode(repository.Snapshot);
            int commits = 0;
            store.ProfileCommitted += _ => commits++;

            Assert.That(ApplicationStashServiceBootstrapper.TryResolveOrphanedReservation(store, admitted),
                Is.EqualTo(StashOperationResult.ReservationPending));

            Assert.That(LocalProfileSaveCodec.Encode(repository.Snapshot), Is.EqualTo(before));
            Assert.That(commits, Is.Zero);
            Assert.That(store.HasCommittedExtraction("raid-a"), Is.True);
            Assert.That(store.HasCommittedExtraction("another-raid"), Is.False);
            store.ClearPendingExtractionCommit();
            Assert.That(store.HasCommittedExtraction("raid-a"), Is.True,
                "A successful backend commit must not turn a later departure into a loss.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OrphanRecovery_MissingReservationRejectsWithoutChanges(bool admitted)
        {
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(_profile, _catalog), Is.True);
            var store = new LocalProfileStore(repository, _profile, _catalog);
            string before = LocalProfileSaveCodec.Encode(repository.Snapshot);
            int commits = 0;
            store.ProfileCommitted += _ => commits++;

            Assert.That(ApplicationStashServiceBootstrapper.TryResolveOrphanedReservation(store, admitted),
                Is.EqualTo(StashOperationResult.InvalidInventory));

            Assert.That(LocalProfileSaveCodec.Encode(repository.Snapshot), Is.EqualTo(before));
            Assert.That(commits, Is.Zero);
            Assert.That(store.HasCommittedExtraction("raid-a"), Is.False);
        }

        [Test]
        public void OrphanRecovery_UnavailableProfileRejectsWithoutPublishing()
        {
            var repository = new InMemoryLocalProfileRepository();
            var store = new LocalProfileStore(repository, _profile, _catalog);
            int commits = 0;
            store.ProfileCommitted += _ => commits++;

            Assert.That(ApplicationStashServiceBootstrapper.TryResolveOrphanedReservation(store, true),
                Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(store.PendingReservation, Is.Null);
            Assert.That(store.GetLoadout(), Is.Empty);
            Assert.That(store.GetStash(), Is.Empty);
            Assert.That(store.HasCommittedExtraction("raid-a"), Is.False);
            Assert.That(commits, Is.Zero);
        }
    }
}
#endif
