#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Stash
{
    /// <summary>
    /// Covers the domain invariant that Stash, Loadout and Equipment cannot be mutated while a raid
    /// reservation owns them, and that these operations fail safely on an unavailable profile.
    /// </summary>
    public sealed class StashReservationInvariantTests
    {
        private const string CatalogPath =
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";
        private const string ProfileValue = "47474747474747474747474747474747";

        private static readonly LootId Sword = new("rapier");
        private static readonly LootId Bone = new("bone");

        private LootDefinitionCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null);
        }

        public delegate StashOperationResult StoreOperation(LocalProfileStore store);

        private static IEnumerable<TestCaseData> ReservedOperations()
        {
            yield return Case("TryTransferToLoadout", s => s.TryTransferToLoadout(Bone, 1));
            yield return Case("TryTransferToStash", s => s.TryTransferToStash(Bone, 1));
            yield return Case("TryTransferAllToLoadout", s => s.TryTransferAllToLoadout());
            yield return Case("TryTransferAllToStash", s => s.TryTransferAllToStash());
            yield return Case(
                "TryEquipFromStash",
                s => s.TryEquipFromStash(EquipmentSlot.WeaponSetAMainHand, Sword));
            yield return Case(
                "TryAssignPreparedEquipment",
                s => s.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, Sword));
            yield return Case(
                "TryClearPreparedEquipment",
                s => s.TryClearPreparedEquipment(EquipmentSlot.WeaponSetAMainHand));
        }

        private static TestCaseData Case(string name, StoreOperation operation) =>
            new TestCaseData(operation).SetName(name);

        [TestCaseSource(nameof(ReservedOperations))]
        public void PendingReservation_RejectsTheOperationWithoutChangingOrPublishingAnything(
            StoreOperation operation)
        {
            LocalProfileStore store = CreateReservedStore();
            Snapshot before = Capture(store);
            int commits = 0;
            store.ProfileCommitted += _ => commits++;

            Assert.That(operation(store), Is.EqualTo(StashOperationResult.ReservationPending));

            Assert.That(Capture(store), Is.EqualTo(before));
            Assert.That(commits, Is.Zero);
        }

        [Test]
        public void PendingReservation_BlockedEquipFromStashDoesNotLoseAUnitOnRollback()
        {
            LocalProfileStore store = CreateStore(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Sword, 3));
            });
            Assert.That(
                store.TryEquipFromStash(EquipmentSlot.WeaponSetAMainHand, Sword),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(store.TryTransferToLoadout(Sword, 1), Is.EqualTo(StashOperationResult.Success));
            Assert.That(CountUnits(store, Sword), Is.EqualTo(3));
            Assert.That(
                store.TryCreateLoadoutReservation("reservation-a", out _),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(
                store.TryEquipFromStash(EquipmentSlot.WeaponSetAMainHand, Sword),
                Is.EqualTo(StashOperationResult.ReservationPending));
            Assert.That(
                store.TryRollbackLoadoutReservation("reservation-a"),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(CountUnits(store, Sword), Is.EqualTo(3));
        }

        [Test]
        public void PendingReservation_BlockedTransferKeepsTheLoadoutEmptyForExtraction()
        {
            LocalProfileStore store = CreateReservedStore();

            Assert.That(
                store.TryTransferAllToLoadout(),
                Is.EqualTo(StashOperationResult.ReservationPending));
            Assert.That(
                store.TryConfirmLoadoutReservation("reservation-a"),
                Is.EqualTo(StashOperationResult.Success));

            var receipt = new ExtractionReceipt("raid-a", new ProfileId(ProfileValue), 1);
            Assert.That(
                store.TryCommitExtraction(receipt, Array.Empty<StashItem>(), default, 0, 1, 0),
                Is.EqualTo(StashOperationResult.Success));
        }

        [Test]
        public void ResolvedReservation_LiftsTheInvariant()
        {
            LocalProfileStore store = CreateReservedStore();
            Assert.That(
                store.TryTransferToLoadout(Bone, 1),
                Is.EqualTo(StashOperationResult.ReservationPending));

            Assert.That(
                store.TryRollbackLoadoutReservation("reservation-a"),
                Is.EqualTo(StashOperationResult.Success));

            Assert.That(store.TryTransferToLoadout(Bone, 1), Is.EqualTo(StashOperationResult.Success));
        }

        [Test]
        public void UnavailableProfile_RejectsTransfersInsteadOfThrowing()
        {
            var repository = new InMemoryLocalProfileRepository();
            var store = new LocalProfileStore(repository, new ProfileId(ProfileValue), _catalog);

            Assert.That(store.IsAvailable, Is.False);
            Assert.That(
                store.TryTransferToLoadout(Bone, 1),
                Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(
                store.TryTransferToStash(Bone, 1),
                Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(
                store.TryTransferAllToLoadout(),
                Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(
                store.TryTransferAllToStash(),
                Is.EqualTo(StashOperationResult.InvalidInventory));
        }

        private LocalProfileStore CreateReservedStore()
        {
            LocalProfileStore store = CreateStore(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 3));
                snapshot.Loadout.Add(new StashItem(Sword, 1));
                snapshot.PreparedEquipment = new PreparedEquipmentLoadout(Sword, default);
            });
            Assert.That(
                store.TryCreateLoadoutReservation("reservation-a", out _),
                Is.EqualTo(StashOperationResult.Success));
            return store;
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

        private static int CountUnits(LocalProfileStore store, LootId lootId)
        {
            int units = 0;
            foreach (StashItem item in store.GetStash()) units += item.LootId == lootId ? item.Amount : 0;
            foreach (StashItem item in store.GetLoadout()) units += item.LootId == lootId ? item.Amount : 0;
            foreach (EquipmentSlot slot in EquipmentSlotRules.AllSlots)
            {
                units += store.GetPreparedEquipment().Get(slot) == lootId ? 1 : 0;
            }

            PendingLoadoutReservation reservation = store.PendingReservation;
            if (reservation != null)
            {
                foreach (StashItem item in reservation.Items) units += item.LootId == lootId ? item.Amount : 0;
                foreach (EquipmentSlot slot in EquipmentSlotRules.AllSlots)
                {
                    units += reservation.PreparedEquipment.Get(slot) == lootId ? 1 : 0;
                }
            }

            return units;
        }

        private static Snapshot Capture(LocalProfileStore store) => new(
            store.GetStash().ToArray(),
            store.GetLoadout().ToArray(),
            EquipmentSlotRules.AllSlots.Select(slot => store.GetPreparedEquipment().Get(slot).Value).ToArray(),
            store.PendingReservation?.ReservationId);

        private readonly struct Snapshot : IEquatable<Snapshot>
        {
            private readonly StashItem[] _stash;
            private readonly StashItem[] _loadout;
            private readonly string[] _equipment;
            private readonly string _reservationId;

            public Snapshot(StashItem[] stash, StashItem[] loadout, string[] equipment, string reservationId)
            {
                _stash = stash;
                _loadout = loadout;
                _equipment = equipment;
                _reservationId = reservationId;
            }

            public bool Equals(Snapshot other) =>
                _stash.SequenceEqual(other._stash) &&
                _loadout.SequenceEqual(other._loadout) &&
                _equipment.SequenceEqual(other._equipment) &&
                _reservationId == other._reservationId;

            public override bool Equals(object obj) => obj is Snapshot other && Equals(other);

            public override int GetHashCode() => _reservationId?.GetHashCode() ?? 0;
        }
    }
}
#endif
