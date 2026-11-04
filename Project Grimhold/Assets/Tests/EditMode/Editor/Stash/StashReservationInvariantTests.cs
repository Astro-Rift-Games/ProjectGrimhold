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
            yield return Case("TryTransferToLoadout_InvalidInput", s => s.TryTransferToLoadout(default, 0));
            yield return Case("TryTransferToStash_InvalidInput", s => s.TryTransferToStash(default, 0));
            yield return Case("EvaluateTransferToLoadout_InvalidInput", s => s.EvaluateTransferToLoadout(default, 0));
            yield return Case("EvaluateTransferToStash_InvalidInput", s => s.EvaluateTransferToStash(default, 0));
            yield return Case("TryTransfer_InvalidStashSlot", s => s.TryTransfer(-1, Bone, 1, true));
            yield return Case("TryTransfer_InvalidLoadoutSlot", s => s.TryTransfer(-1, Bone, 1, false));
            yield return Case("EvaluateTransfer_InvalidStashSlot", s => s.EvaluateTransfer(-1, Bone, 1, true));
            yield return Case("EvaluateTransfer_InvalidLoadoutSlot", s => s.EvaluateTransfer(-1, Bone, 1, false));
            yield return Case("TryTransfer_InvalidItem", s => s.TryTransfer(0, default, 1, true));
            yield return Case("EvaluateTransfer_InvalidAmount", s => s.EvaluateTransfer(0, Bone, 0, true));
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

        [TestCase(true)]
        [TestCase(false)]
        public void Transfers_WithoutReservation_KeepReceiveAndValidationBehavior(bool fromStash)
        {
            LocalProfileStore store = CreateStore(snapshot =>
            {
                snapshot.Stash.Add(new StashItem(Bone, 3, fromStash ? 4 : 1));
                snapshot.Loadout.Add(new StashItem(Bone, 2, fromStash ? 1 : 4));
            });
            Snapshot before = Capture(store);
            int commits = 0;
            store.ProfileCommitted += _ => commits++;

            Assert.That(store.EvaluateTransfer(-1, Bone, 1, fromStash), Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(store.TryTransfer(-1, Bone, 1, fromStash), Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(fromStash ? store.EvaluateTransferToLoadout(default, 0) : store.EvaluateTransferToStash(default, 0),
                Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(fromStash ? store.TryTransferToLoadout(default, 0) : store.TryTransferToStash(default, 0),
                Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(Capture(store), Is.EqualTo(before));
            Assert.That(commits, Is.Zero);

            Assert.That(store.EvaluateTransfer(4, Bone, int.MaxValue, fromStash), Is.EqualTo(StashOperationResult.Success));
            Assert.That(Capture(store), Is.EqualTo(before));
            Assert.That(commits, Is.Zero);
            Assert.That(fromStash ? store.TryTransferToLoadout(Bone, int.MaxValue) : store.TryTransferToStash(Bone, int.MaxValue),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(fromStash ? store.GetStash() : store.GetLoadout(), Is.Empty);
            Assert.That(fromStash ? store.GetLoadout() : store.GetStash(),
                Is.EqualTo(new[] { new StashItem(Bone, 5, 1) }));
            Assert.That(commits, Is.EqualTo(1));
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
            PhysicalSlotTestSeed.AssignUnplaced(snapshot);
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
            store.PendingReservation?.ReservationId,
            store.PendingReservation?.Items.ToArray() ?? Array.Empty<StashItem>(),
            store.PendingReservation?.PreparedEquipment ?? default);

        private readonly struct Snapshot : IEquatable<Snapshot>
        {
            private readonly StashItem[] _stash;
            private readonly StashItem[] _loadout;
            private readonly string[] _equipment;
            private readonly string _reservationId;
            private readonly StashItem[] _reservedItems;
            private readonly PreparedEquipmentLoadout _reservedEquipment;

            public Snapshot(StashItem[] stash, StashItem[] loadout, string[] equipment, string reservationId,
                StashItem[] reservedItems, PreparedEquipmentLoadout reservedEquipment)
            {
                _stash = stash;
                _loadout = loadout;
                _equipment = equipment;
                _reservationId = reservationId;
                _reservedItems = reservedItems;
                _reservedEquipment = reservedEquipment;
            }

            public bool Equals(Snapshot other) =>
                _stash.SequenceEqual(other._stash) &&
                _loadout.SequenceEqual(other._loadout) &&
                _equipment.SequenceEqual(other._equipment) &&
                _reservationId == other._reservationId &&
                _reservedItems.SequenceEqual(other._reservedItems) &&
                _reservedEquipment.Equals(other._reservedEquipment);

            public override bool Equals(object obj) => obj is Snapshot other && Equals(other);

            public override int GetHashCode() => _reservationId?.GetHashCode() ?? 0;
        }
    }
}
#endif
