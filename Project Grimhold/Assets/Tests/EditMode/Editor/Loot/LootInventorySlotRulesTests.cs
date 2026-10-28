using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Loot
{
    public sealed class LootInventorySlotRulesTests
    {
        private LootInventorySlot[] _slots;

        [SetUp]
        public void SetUp() => _slots = new LootInventorySlot[LootInventoryRules.PhysicalSlotCount];

        [Test]
        public void DefaultSlot_IsEmptyButCatalogZeroCanBeOccupied()
        {
            Assert.That(default(LootInventorySlot).IsEmpty, Is.True);
            Assert.That(default(LootInventorySlot).IsValid, Is.False);
            Assert.That(default(LootInventorySlot).Origin.IsValid, Is.False);
            LootInventorySlot slot = Slot(0, 1);
            Assert.That(slot.IsEmpty, Is.False);
            Assert.That(slot.IsValid, Is.True);
            Assert.That(slot.CatalogIndex, Is.Zero);
            Assert.That(slot.Origin, Is.EqualTo(RaidLootOrigin.Dungeon));
            Assert.That(typeof(INetworkStruct).IsAssignableFrom(typeof(LootInventorySlot)), Is.True);
        }

        [TestCase(1)]
        [TestCase(16)]
        public void Slot_PlayerOriginRoundTripsExactParticipant(int participant)
        {
            Assert.That(Slot(127, 2, Player(participant)).Origin, Is.EqualTo(Player(participant)));
        }

        [TestCase(-1, 1)]
        [TestCase(128, 1)]
        [TestCase(0, 0)]
        [TestCase(0, -1)]
        public void Slot_InvalidIndexOrAmount_IsRejected(int catalogIndex, int quantity)
        {
            Assert.That(LootInventorySlot.TryCreate(catalogIndex, quantity, RaidLootOrigin.Dungeon, out var slot), Is.False);
            Assert.That(slot.IsEmpty, Is.True);
        }

        [Test]
        public void Slot_InvalidOrigin_IsRejected()
        {
            Assert.That(LootInventorySlot.TryCreate(0, 1, default, out _), Is.False);
        }

        [Test]
        public void Receive_AboveStackMax_PartitionsAcrossSlots()
        {
            Assert.That(Receive(25, 10), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(_slots[0].Quantity, Is.EqualTo(10));
            Assert.That(_slots[1].Quantity, Is.EqualTo(10));
            Assert.That(_slots[2].Quantity, Is.EqualTo(5));
            Assert.That(_slots[3].IsEmpty, Is.True);
        }

        [Test]
        public void Receive_FillsCompatiblePartialStacksBeforeAscendingHoles()
        {
            _slots[3] = Slot(0, 8);
            _slots[7] = Slot(0, 6);
            Assert.That(Receive(19, 10), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(_slots[3].Quantity, Is.EqualTo(10));
            Assert.That(_slots[7].Quantity, Is.EqualTo(10));
            Assert.That(_slots[0].Quantity, Is.EqualTo(10));
            Assert.That(_slots[1].Quantity, Is.EqualTo(3));
            Assert.That(_slots[2].IsEmpty, Is.True);
        }

        [Test]
        public void Receive_Nonstackables_UseOneSlotPerUnit()
        {
            Assert.That(Receive(3, 1), Is.EqualTo(LootTransferFailureReason.None));
            for (int index = 0; index < 3; index++)
                Assert.That(_slots[index].Quantity, Is.EqualTo(1));
        }

        [Test]
        public void Receive_MixedOriginsAndDefinitions_NeverMerge()
        {
            _slots[0] = Slot(0, 2);
            _slots[1] = Slot(0, 2, Player(1));
            _slots[2] = Slot(0, 2, Player(2));
            _slots[3] = Slot(1, 2, Player(1));
            Assert.That(LootInventoryRules.TryReceive(_slots, 0, Player(1), 5, 0, 4), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(_slots[0].Quantity, Is.EqualTo(2));
            Assert.That(_slots[1].Quantity, Is.EqualTo(5));
            Assert.That(_slots[2].Quantity, Is.EqualTo(2));
            Assert.That(_slots[3].Quantity, Is.EqualTo(2));
            Assert.That(_slots[4], Is.EqualTo(Slot(0, 1, Player(1))));
            Assert.That(_slots[0].CanStackWith(_slots[1]), Is.False);
            Assert.That(_slots[1].CanStackWith(_slots[2]), Is.False);
            Assert.That(_slots[1].CanStackWith(_slots[3]), Is.False);
            Assert.That(_slots[1].CanStackWith(_slots[4]), Is.True);
            Assert.That(default(LootInventorySlot).CanStackWith(_slots[1]), Is.False);
        }

        [Test]
        public void Receive_FullThirtySlots_RejectsWithoutMutation()
        {
            Fill(10);
            var before = (LootInventorySlot[])_slots.Clone();
            Assert.That(Receive(1, 10), Is.EqualTo(LootTransferFailureReason.InventoryFull));
            Assert.That(_slots, Is.EqualTo(before));
        }

        [Test]
        public void Capacity_ReturnsPartialButFullCommitIsAtomic()
        {
            Fill(10);
            _slots[4] = Slot(0, 8);
            _slots[8] = default;
            var before = (LootInventorySlot[])_slots.Clone();
            Assert.That(Capacity(20, 10, 0, out int amount), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(amount, Is.EqualTo(12));
            Assert.That(_slots, Is.EqualTo(before));
            Assert.That(LootInventoryRules.ValidateReceive(_slots, 0, RaidLootOrigin.Dungeon, 10, 0, 20),
                Is.EqualTo(LootTransferFailureReason.InventoryFull));
            Assert.That(Receive(20, 10), Is.EqualTo(LootTransferFailureReason.InventoryFull));
            Assert.That(_slots, Is.EqualTo(before));
            Assert.That(Receive(amount, 10), Is.EqualTo(LootTransferFailureReason.None));
        }

        [Test]
        public void Receive_FullSlotsWithOnlyIncompatibleRoom_Rejects()
        {
            Fill(2, Player(2));
            Assert.That(Receive(1, 10), Is.EqualTo(LootTransferFailureReason.InventoryFull));
            Assert.That(Capacity(1, 10, 0, out int amount), Is.EqualTo(LootTransferFailureReason.InventoryFull));
            Assert.That(amount, Is.Zero);
        }

        [Test]
        public void MaxTransportable_CountsAllOriginsAndSlotsButNotOtherItems()
        {
            _slots[0] = Slot(0, 3);
            _slots[1] = Slot(0, 4, Player(1));
            _slots[2] = Slot(0, 2, Player(2));
            _slots[3] = Slot(1, 50);
            var before = (LootInventorySlot[])_slots.Clone();
            Assert.That(Capacity(5, 10, 12, out int amount), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(amount, Is.EqualTo(3));
            Assert.That(Receive(5, 10, 12), Is.EqualTo(LootTransferFailureReason.MaxTransportableExceeded));
            Assert.That(_slots, Is.EqualTo(before));
            Assert.That(Receive(3, 10, 12), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(Capacity(1, 10, 12, out amount), Is.EqualTo(LootTransferFailureReason.MaxTransportableExceeded));
            Assert.That(amount, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Receive_InvalidQuantity_RejectsWithoutMutation(int quantity)
        {
            Assert.That(Receive(quantity, 10), Is.EqualTo(LootTransferFailureReason.InvalidAmount));
            Assert.That(_slots[0].IsEmpty, Is.True);
        }

        [TestCase(0, 0)]
        [TestCase(-1, 0)]
        [TestCase(10, -1)]
        public void Receive_InvalidConfiguration_IsUnavailable(int stackMax, int maxTransportable)
        {
            Assert.That(Receive(1, stackMax, maxTransportable), Is.EqualTo(LootTransferFailureReason.ContainerUnavailable));
            Assert.That(_slots[0].IsEmpty, Is.True);
        }

        [TestCase(0)]
        [TestCase(29)]
        [TestCase(31)]
        public void Receive_RequiresExactlyThirtySlots(int length)
        {
            _slots = new LootInventorySlot[length];
            Assert.That(Receive(1, 10), Is.EqualTo(LootTransferFailureReason.ContainerUnavailable));
        }

        [Test]
        public void MissingStorage_RejectsReceiveAndExtraction()
        {
            _slots = null;
            Assert.That(Receive(1, 10), Is.EqualTo(LootTransferFailureReason.ContainerUnavailable));
            Assert.That(LootInventoryRules.TryExtract(_slots, 0, 1), Is.EqualTo(LootTransferFailureReason.ContainerUnavailable));
        }

        [Test]
        public void Receive_InvalidLootOrOrigin_Rejects()
        {
            Assert.That(LootInventoryRules.TryReceive(_slots, -1, RaidLootOrigin.Dungeon, 10, 0, 1), Is.EqualTo(LootTransferFailureReason.InvalidLoot));
            Assert.That(LootInventoryRules.TryReceive(_slots, 128, RaidLootOrigin.Dungeon, 10, 0, 1), Is.EqualTo(LootTransferFailureReason.InvalidLoot));
            Assert.That(LootInventoryRules.TryReceive(_slots, 0, default, 10, 0, 1), Is.EqualTo(LootTransferFailureReason.InvalidLoot));
        }

        [Test]
        public void Receive_ExistingStackExceedsConfiguredMax_FailsClosed()
        {
            _slots[0] = Slot(0, 11);
            var before = (LootInventorySlot[])_slots.Clone();
            Assert.That(Receive(1, 10), Is.EqualTo(LootTransferFailureReason.ContainerUnavailable));
            Assert.That(_slots, Is.EqualTo(before));
        }

        [Test]
        public void Receive_IntegerOverflow_IsRejectedAndCapacityIsSafe()
        {
            _slots[0] = Slot(0, int.MaxValue - 1);
            var before = (LootInventorySlot[])_slots.Clone();
            Assert.That(Receive(2, int.MaxValue), Is.EqualTo(LootTransferFailureReason.Overflow));
            Assert.That(_slots, Is.EqualTo(before));
            Assert.That(Capacity(2, int.MaxValue, 0, out int amount), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(amount, Is.EqualTo(1));
            Assert.That(Receive(1, int.MaxValue), Is.EqualTo(LootTransferFailureReason.None));
        }

        [Test]
        public void Receive_ExistingAggregateOverflow_FailsClosed()
        {
            _slots[0] = Slot(0, int.MaxValue);
            _slots[1] = Slot(0, 1, Player(1));
            Assert.That(Capacity(1, int.MaxValue, 0, out int amount), Is.EqualTo(LootTransferFailureReason.Overflow));
            Assert.That(amount, Is.Zero);
        }

        [Test]
        public void Extraction_PartialThenComplete_PreservesHolesAndOrigin()
        {
            _slots[4] = Slot(0, 3, Player(16));
            _slots[8] = Slot(1, 2);
            Assert.That(LootInventoryRules.TryExtract(_slots, 4, 2), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(_slots[4], Is.EqualTo(Slot(0, 1, Player(16))));
            Assert.That(LootInventoryRules.TryExtract(_slots, 4, 1), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(_slots[4].IsEmpty, Is.True);
            Assert.That(_slots[8], Is.EqualTo(Slot(1, 2)));
            Assert.That(_slots[0].IsEmpty, Is.True);
        }

        [TestCase(4, 4, LootTransferFailureReason.InsufficientAmount)]
        [TestCase(4, 0, LootTransferFailureReason.InvalidAmount)]
        [TestCase(4, -1, LootTransferFailureReason.InvalidAmount)]
        [TestCase(5, 1, LootTransferFailureReason.InsufficientAmount)]
        [TestCase(-1, 1, LootTransferFailureReason.InvalidLoot)]
        [TestCase(30, 1, LootTransferFailureReason.InvalidLoot)]
        public void Extraction_InvalidRequest_IsAtomic(int index, int amount, LootTransferFailureReason expected)
        {
            _slots[4] = Slot(0, 3);
            var before = (LootInventorySlot[])_slots.Clone();
            Assert.That(LootInventoryRules.TryExtract(_slots, index, amount), Is.EqualTo(expected));
            Assert.That(_slots, Is.EqualTo(before));
        }

        private LootTransferFailureReason Receive(int quantity, int stackMax, int maxTransportable = 0) =>
            LootInventoryRules.TryReceive(_slots, 0, RaidLootOrigin.Dungeon, stackMax, maxTransportable, quantity);

        private LootTransferFailureReason Capacity(int quantity, int stackMax, int maxTransportable, out int amount) =>
            LootInventoryRules.GetReceivableAmount(_slots, 0, RaidLootOrigin.Dungeon, stackMax, maxTransportable, quantity, out amount);

        private void Fill(int quantity, RaidLootOrigin origin = default)
        {
            for (int index = 0; index < _slots.Length; index++)
                _slots[index] = Slot(0, quantity, origin);
        }

        private static LootInventorySlot Slot(int catalogIndex, int quantity, RaidLootOrigin origin = default)
        {
            if (!origin.IsValid)
                origin = RaidLootOrigin.Dungeon;
            Assert.That(LootInventorySlot.TryCreate(catalogIndex, quantity, origin, out var slot), Is.True);
            return slot;
        }

        private static RaidLootOrigin Player(int value)
        {
            Assert.That(RaidParticipantId.TryCreate(value, out var id), Is.True);
            Assert.That(RaidLootOrigin.TryCreatePlayer(id, out var origin), Is.True);
            return origin;
        }
    }
}
