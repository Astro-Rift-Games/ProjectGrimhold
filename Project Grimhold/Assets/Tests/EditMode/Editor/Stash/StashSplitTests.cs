#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Stash
{
    public sealed class StashSplitTests
    {
        private static readonly LootId Bone = new("bone");
        private static readonly LootId Sword = new("rapier");
        private readonly ProfileId _profile = new("49494949494949494949494949494949");
        private LocalProfileStore _store;
        private int _commits;

        private void Create(InventoryList list, Action<LocalProfileSnapshot, List<StashItem>> configure = null)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(_profile, catalog), Is.True);
            var snapshot = repository.Snapshot.Clone();
            snapshot.InventorySchemaVersion = 2;
            snapshot.RemoteRevision = 7;
            var source = list == InventoryList.Stash ? snapshot.Stash : snapshot.Loadout;
            var other = list == InventoryList.Stash ? snapshot.Loadout : snapshot.Stash;
            source.Add(new StashItem(Bone, 13, 9));
            source.Add(new StashItem(Sword, 2, 17));
            other.Add(new StashItem(Bone, 4, 3));
            configure?.Invoke(snapshot, source);
            Assert.That(repository.TrySave(snapshot, out var error), Is.True, error);
            _store = new LocalProfileStore(repository, _profile, catalog);
            _commits = 0;
            _store.ProfileCommitted += _ => _commits++;
        }

        private IReadOnlyList<StashItem> Items(InventoryList list) =>
            list == InventoryList.Stash ? _store.GetStash() : _store.GetLoadout();

        [TestCase(InventoryList.Stash, false)]
        [TestCase(InventoryList.Loadout, false)]
        [TestCase(InventoryList.Stash, true)]
        [TestCase(InventoryList.Loadout, true)]
        public void Split_INV_AC_03_ConservesThirteenAndOnlyChangesAddressedStack(InventoryList list, bool mixed)
        {
            Create(list, (_, source) =>
            {
                if (mixed) source.Add(new StashItem(Bone, 7, 1));
            });
            var stashBefore = _store.GetStash().ToArray();
            var loadoutBefore = _store.GetLoadout().ToArray();
            var untouched = Items(list).Where(item => item.Slot != 9).ToArray();
            int total = Items(list).Sum(item => item.Amount);
            Assert.That(_store.EvaluateSplit(list, 9, 8, 5, Bone), Is.EqualTo(StashOperationResult.Success));
            Assert.That(_store.GetStash(), Is.EqualTo(stashBefore));
            Assert.That(_store.GetLoadout(), Is.EqualTo(loadoutBefore));
            Assert.That(_commits, Is.Zero);

            Assert.That(_store.TrySplit(list, 9, 8, 5, Bone), Is.EqualTo(StashOperationResult.Success));

            Assert.That(Items(list).Single(item => item.Slot == 9), Is.EqualTo(new StashItem(Bone, 5, 9)));
            Assert.That(Items(list).Single(item => item.Slot == 5), Is.EqualTo(new StashItem(Bone, 8, 5)));
            Assert.That(Items(list).Where(item => item.Slot != 9 && item.Slot != 5), Is.EquivalentTo(untouched));
            Assert.That(Items(list).Sum(item => item.Amount), Is.EqualTo(total));
            Assert.That(list == InventoryList.Stash ? _store.GetLoadout() : _store.GetStash(),
                Is.EqualTo(list == InventoryList.Stash ? loadoutBefore : stashBefore));
            AssertUnrelatedState();
            Assert.That(_commits, Is.EqualTo(1));
        }

        private static IEnumerable<TestCaseData> InvalidCases()
        {
            foreach (InventoryList list in new[] { InventoryList.Stash, InventoryList.Loadout })
            foreach (string invalid in new[] { "zero", "negative", "all", "excess", "singleSource", "occupied", "occupiedSameId", "same", "negativeDestination",
                "capacity", "full", "overfull", "missing", "negativeSource", "mismatch", "invalidId", "invalidList" })
                yield return new TestCaseData(list, invalid);
        }

        [TestCaseSource(nameof(InvalidCases))]
        public void Split_InvalidInput_IsAtomicAndEvaluationMatchesCommit(InventoryList list, string invalid)
        {
            int capacity = list == InventoryList.Stash ? 60 : 30;
            Create(list, (_, source) =>
            {
                if (invalid == "singleSource") source[0] = new StashItem(Bone, 1, 9);
                if (invalid == "occupiedSameId") source[1] = new StashItem(Bone, 2, 17);
                if (invalid != "full" && invalid != "overfull") return;
                // A free in-capacity destination still cannot grow a list whose overflow
                // entries already bring its occupied count to capacity. Duplicate IDs count.
                source.Clear();
                source.Add(new StashItem(Bone, 13, 9));
                int occupied = invalid == "overfull" ? capacity + 1 : capacity;
                for (int index = 0; index < occupied - 1; index++)
                    source.Add(new StashItem(Bone, 1, capacity + index));
            });
            int amount = invalid switch { "zero" => 0, "negative" => -1, "all" => 13, "excess" => 14, "singleSource" => 1, _ => 8 };
            int destination = invalid switch { "occupied" => 17, "occupiedSameId" => 17, "same" => 9, "negativeDestination" => -1, "capacity" => capacity, _ => 5 };
            int sourceSlot = invalid switch { "missing" => 8, "negativeSource" => -1, _ => 9 };
            LootId guard = invalid == "mismatch" ? Sword : invalid == "invalidId" ? default : Bone;
            InventoryList requestedList = invalid == "invalidList" ? (InventoryList)99 : list;
            StashOperationResult expected = invalid == "capacity" || invalid == "full" || invalid == "overfull"
                ? StashOperationResult.InventoryFull : StashOperationResult.InvalidInventory;
            AssertRejectedUnchanged(requestedList, sourceSlot, amount, destination, guard, expected);
        }

        [TestCase(InventoryList.Stash)]
        [TestCase(InventoryList.Loadout)]
        public void Split_ReservationPending_PrecedesEveryInputValidation(InventoryList list)
        {
            Create(list, (snapshot, _) => snapshot.PendingReservation =
                new PendingLoadoutReservation("split-reserved", Array.Empty<StashItem>()));
            AssertRejectedUnchanged(list, 9, 8, 5, Bone, StashOperationResult.ReservationPending);
            AssertRejectedUnchanged((InventoryList)99, -1, 0, -1, default, StashOperationResult.ReservationPending);
            Assert.That(_store.PendingReservation.ReservationId, Is.EqualTo("split-reserved"));
        }

        [TestCase(InventoryList.Stash)]
        [TestCase(InventoryList.Loadout)]
        public void Split_OverflowSource_CanReduceIntoLastInCapacitySlot(InventoryList list)
        {
            int capacity = list == InventoryList.Stash ? 60 : 30;
            Create(list, (_, source) => source[0] = new StashItem(Bone, 13, capacity + 2));
            Assert.That(_store.TrySplit(list, capacity + 2, 12, capacity - 1, Bone), Is.EqualTo(StashOperationResult.Success));
            Assert.That(Items(list).Single(item => item.Slot == capacity + 2).Amount, Is.EqualTo(1));
            Assert.That(Items(list).Single(item => item.Slot == capacity - 1), Is.EqualTo(new StashItem(Bone, 12, capacity - 1)));
            Assert.That(_commits, Is.EqualTo(1));
        }

        [TestCase(InventoryList.Stash)]
        [TestCase(InventoryList.Loadout)]
        public void Split_OneUnit_IsAllowedWithoutStackMax(InventoryList list)
        {
            // Town does not enforce the definition's nonstackable/StackMax metadata.
            Create(list, (_, source) => source[0] = new StashItem(Sword, 13, 9));
            Assert.That(_store.TrySplit(list, 9, 1, 5, Sword), Is.EqualTo(StashOperationResult.Success));
            Assert.That(Items(list).Single(item => item.Slot == 9), Is.EqualTo(new StashItem(Sword, 12, 9)));
            Assert.That(Items(list).Single(item => item.Slot == 5), Is.EqualTo(new StashItem(Sword, 1, 5)));
            Assert.That(_commits, Is.EqualTo(1));
        }

        [Test]
        public void SplitRequest_SerializesExactBackendContract()
        {
            string json = InventoryRequestJson.Encode(new InventorySlotSplitRequest
            {
                list = "stash", sourceSlot = 9, amount = 8, destinationSlot = 1, lootId = "bone", expectedRevision = 4
            });
            Assert.That(json, Is.EqualTo("{\"list\":\"stash\",\"sourceSlot\":9,\"amount\":8,\"destinationSlot\":1,\"lootId\":\"bone\",\"expectedRevision\":4}"));
        }

        [Test]
        public void SplitTransport_UsesProtocolTwoHeader()
        {
            using var request = InventoryProtocol.Create("https://example.invalid/character/me/inventory/slots/split", "POST");
            Assert.That(request.GetRequestHeader("X-Grimhold-Inventory-Protocol"), Is.EqualTo("2"));
        }

        private void AssertRejectedUnchanged(InventoryList list, int source, int amount, int destination, LootId guard,
            StashOperationResult expected)
        {
            var stash = _store.GetStash().ToArray();
            var loadout = _store.GetLoadout().ToArray();
            Assert.That(_store.EvaluateSplit(list, source, amount, destination, guard), Is.EqualTo(expected));
            Assert.That(_store.TrySplit(list, source, amount, destination, guard), Is.EqualTo(expected));
            Assert.That(_store.GetStash(), Is.EqualTo(stash));
            Assert.That(_store.GetLoadout(), Is.EqualTo(loadout));
            AssertUnrelatedState();
            Assert.That(_commits, Is.Zero);
        }

        private void AssertUnrelatedState()
        {
            Assert.That(_store.RemoteRevision, Is.EqualTo(7));
            Assert.That(_store.InventorySchemaVersion, Is.EqualTo(2));
            Assert.That(_store.GetCurrency(), Is.EqualTo(LocalProfileSnapshot.InitialCurrency));
            Assert.That(_store.GetPreparedEquipment().HasAnyEquipment, Is.False);
            Assert.That(_store.PendingExtractionCommit, Is.Null);
        }
    }
}
#endif
