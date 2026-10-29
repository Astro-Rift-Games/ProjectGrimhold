using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class RaidLootSelectionStateTests
    {
        [Test]
        public void Selection_IsPreservedWhileStackExistsAndClearedWhenItDisappears()
        {
            var selection = new RaidLootSelectionState();
            var selectedId = new LootId("coin");
            var entries = new List<LootEntry>
            {
                new(selectedId, 3),
                new(new LootId("potion"), 1)
            };

            Assert.That(selection.TrySelect(selectedId, entries), Is.True);
            selection.Reconcile(entries);
            Assert.That(selection.SelectedLootId, Is.EqualTo(selectedId));

            entries.RemoveAt(0);
            selection.Reconcile(entries);
            Assert.That(selection.HasSelection, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PhysicalSelection_DistinguishesSecondSameItemAddressRegardlessOfOrigin(bool differentOrigin)
        {
            var selection = new RaidLootSelectionState();
            RaidInventorySlotData[] slots = PhysicalSlots(19, differentOrigin);
            Assert.That(selection.TrySelect(7, 19, slots), Is.True);
            Assert.That(selection.SelectedLootId, Is.EqualTo(new LootId("coin")));
            Assert.That(selection.SelectedInventorySlotIndex, Is.EqualTo(7));
            Assert.That(selection.ExpectedInventoryRevision, Is.EqualTo(19));
            selection.Reconcile(slots);
            Assert.That(selection.SelectedInventorySlotIndex, Is.EqualTo(7));
            selection.Clear();
            Assert.That(selection.HasSelection, Is.False);
            Assert.That(selection.SelectedInventorySlotIndex, Is.EqualTo(-1));
            Assert.That(selection.ExpectedInventoryRevision, Is.EqualTo(-1));
        }

        [Test]
        public void PhysicalSelection_NewRevisionClearsEvenWhenSameItemAndAddressStillExist()
        {
            var selection = new RaidLootSelectionState();
            Assert.That(selection.TrySelect(7, 19, PhysicalSlots(19, false)), Is.True);
            RaidInventorySlotData[] refreshed = PhysicalSlots(20, false);
            selection.Reconcile(refreshed);
            Assert.That(selection.HasSelection, Is.False);
            Assert.That(selection.SelectedInventorySlotIndex, Is.EqualTo(-1));
            Assert.That(selection.ExpectedInventoryRevision, Is.EqualTo(-1));
            Assert.That(selection.TrySelect(7, 19, refreshed), Is.False, "Never substitute the refreshed revision.");
        }

        [Test]
        public void PhysicalSelection_EmptySelectedAddressDoesNotRetargetAnotherMatchingItem()
        {
            var selection = new RaidLootSelectionState();
            RaidInventorySlotData[] slots = PhysicalSlots(19, false);
            Assert.That(selection.TrySelect(7, 19, slots), Is.True);
            slots[7] = RaidInventorySlotData.EmptyInventorySlot(7, 19);
            selection.Reconcile(slots);
            Assert.That(selection.HasSelection, Is.False);
            Assert.That(selection.TrySelect(7, 19, slots), Is.False);
            Assert.That(selection.TrySelect(-1, 19, slots), Is.False);
            Assert.That(selection.TrySelect(30, 19, slots), Is.False);
            Assert.That(slots[0].IsOccupied, Is.True, "The same ID elsewhere cannot rescue a stale address.");
        }

        [Test]
        public void LegacySelection_RemainsItemBasedAndUnaddressed()
        {
            var selection = new RaidLootSelectionState();
            var entries = new[] { new LootEntry(new LootId("coin"), 13) };
            Assert.That(selection.TrySelect(entries[0].LootId, entries), Is.True);
            selection.Reconcile(entries);
            Assert.That(selection.HasSelection, Is.True);
            Assert.That(selection.SelectedInventorySlotIndex, Is.EqualTo(-1));
            Assert.That(selection.ExpectedInventoryRevision, Is.EqualTo(-1));
        }

        private static RaidInventorySlotData[] PhysicalSlots(int revision, bool differentOrigin)
        {
            var slots = new RaidInventorySlotData[30];
            for (int index = 0; index < slots.Length; index++)
                slots[index] = RaidInventorySlotData.EmptyInventorySlot(index, revision);
            RaidParticipantId.TryCreate(2, out RaidParticipantId participant);
            RaidLootOrigin.TryCreatePlayer(participant, out RaidLootOrigin playerOrigin);
            var id = new LootId("coin");
            slots[0] = RaidInventorySlotData.CreateInventorySlot(new LootEntry(id, 10), null, null,
                0, revision, RaidLootOrigin.Dungeon);
            slots[7] = RaidInventorySlotData.CreateInventorySlot(new LootEntry(id, 3), null, null,
                7, revision, differentOrigin ? playerOrigin : RaidLootOrigin.Dungeon);
            return slots;
        }

        [Test]
        public void EmptyOrMissingStack_CannotBeSelected()
        {
            var selection = new RaidLootSelectionState();
            var entries = new List<LootEntry> { new(new LootId("potion"), 1) };

            Assert.That(selection.TrySelect(default, entries), Is.False);
            Assert.That(selection.TrySelect(new LootId("coin"), entries), Is.False);
            Assert.That(selection.HasSelection, Is.False);
        }
    }
}
