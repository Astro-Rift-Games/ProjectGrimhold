using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class LootInventoryRulesTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void CanDiscardItem_FollowsDefinitionCanBeDiscarded(bool canBeDiscarded)
        {
            var definition = ScriptableObject.CreateInstance<LootDefinition>();
            try
            {
                FieldInfo field = typeof(LootDefinition).GetField("_canBeDiscarded",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                field.SetValue(definition, canBeDiscarded);

                Assert.That(LootInventoryRules.CanDiscardItem(definition), Is.EqualTo(canBeDiscarded));
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void CanDiscardItem_MissingDefinition_IsRejected()
        {
            Assert.That(LootInventoryRules.CanDiscardItem(null), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CanTransferItem_FollowsDefinitionCanBeTransferred(bool canBeTransferred)
        {
            var definition = ScriptableObject.CreateInstance<LootDefinition>();
            try
            {
                FieldInfo field = typeof(LootDefinition).GetField("_canBeTransferred",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                field.SetValue(definition, canBeTransferred);

                Assert.That(LootInventoryRules.CanTransferItem(definition), Is.EqualTo(canBeTransferred));
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void CanTransferItem_MissingDefinition_IsRejected()
        {
            Assert.That(LootInventoryRules.CanTransferItem(null), Is.False);
        }

        [Test]
        public void NotTransferable_IsRecognizedAsRejectionReason()
        {
            LootTransferResult result = LootTransferResult.Rejected(LootTransferFailureReason.NotTransferable);
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(LootTransferFailureReason.NotTransferable));
        }

        [Test]
        public void WeaponReceptionExtractionAndRejectedSplit_KeepParallelModifiersInLockstep()
        {
            var slots = new LootInventorySlot[30];
            var modifiers = new WeaponInstanceModifiersNetworkData[30];
            var first = WeaponInstanceModifiersNetworkData.FromPacked(2 << 12);
            var second = WeaponInstanceModifiersNetworkData.FromPacked(1 << 12);
            Assert.That(LootInventoryRules.TryReceive(slots, modifiers, 0, RaidLootOrigin.Dungeon,
                1, 0, 1, true, first), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(LootInventoryRules.TryReceive(slots, modifiers, 0, RaidLootOrigin.Dungeon,
                1, 0, 1, true, second), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(slots[0].Quantity, Is.EqualTo(1));
            Assert.That(slots[1].Quantity, Is.EqualTo(1));
            Assert.That(modifiers[0], Is.EqualTo(first));
            Assert.That(modifiers[1], Is.EqualTo(second));
            var before = (LootInventorySlot[])slots.Clone();
            var modifiersBefore = (WeaponInstanceModifiersNetworkData[])modifiers.Clone();
            Assert.That(LootInventoryRules.TrySplit(slots, modifiers, 0, 1, 2), Is.EqualTo(LootTransferFailureReason.InvalidAmount));
            Assert.That(slots, Is.EqualTo(before));
            Assert.That(modifiers, Is.EqualTo(modifiersBefore));
            Assert.That(LootInventoryRules.TryExtract(slots, modifiers, 1, 1), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(slots[1].IsEmpty, Is.True);
            Assert.That(modifiers[1].IsEmpty, Is.True);
            Assert.That(modifiers[0], Is.EqualTo(first));
            Assert.That(LootInventoryRules.TryReceive(slots, modifiers, 1, RaidLootOrigin.Dungeon,
                10, 0, 3, false, default), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(modifiers[1].IsEmpty, Is.True, "A reused non-weapon slot must not retain a weapon's modifiers.");
            Assert.That(LootInventoryRules.TrySplit(slots, modifiers, 1, 1, 2), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(slots[1].Quantity, Is.EqualTo(2));
            Assert.That(slots[2].Quantity, Is.EqualTo(1));
            Assert.That(modifiers[1].IsEmpty && modifiers[2].IsEmpty, Is.True);
        }

        [Test]
        public void WeaponBulkReceptionOrNonWeaponModifiers_RejectWithoutMutation()
        {
            var slots = new LootInventorySlot[30];
            var modifiers = new WeaponInstanceModifiersNetworkData[30];
            var weapon = WeaponInstanceModifiersNetworkData.FromPacked(2 << 12);
            Assert.That(LootInventoryRules.TryReceive(slots, modifiers, 0, RaidLootOrigin.Dungeon,
                10, 0, 2, true, weapon), Is.EqualTo(LootTransferFailureReason.InvalidAmount));
            Assert.That(LootInventoryRules.TryReceive(slots, modifiers, 0, RaidLootOrigin.Dungeon,
                10, 0, 1, false, weapon), Is.EqualTo(LootTransferFailureReason.InvalidLoot));
            Assert.That(slots, Is.EqualTo(new LootInventorySlot[30]));
            Assert.That(modifiers, Is.EqualTo(new WeaponInstanceModifiersNetworkData[30]));
        }

        [TestCase(1, true)]
        [TestCase(16, true)]
        [TestCase(30, true)]
        [TestCase(31, false)]
        [TestCase(64, false)]
        [TestCase(0, false)]
        [TestCase(-1, false)]
        [TestCase(65, false)]
        public void SlotCapacity_MustFitTechnicalRepresentation(int slotCapacity, bool expected)
        {
            Assert.That(
                LootInventoryRules.IsValidSlotCapacity(slotCapacity, PlayerLootReceiver.MaxDistinctLootTypes),
                Is.EqualTo(expected));
        }

        [Test]
        public void Receive_NewLootWithFreeSlot_IsAccepted()
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateReceive(
                false,
                0,
                0,
                2,
                3);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.None));
        }

        [Test]
        public void Receive_ExistingLoot_StacksWithoutUsingAnotherSlot()
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateReceive(
                true,
                4,
                2,
                2,
                3);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(LootInventoryRules.CalculateReceivedAmount(4, 3), Is.EqualTo(7));
        }

        [Test]
        public void Receive_NewLootAtSlotCapacity_IsInventoryFull()
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateReceive(
                false,
                0,
                2,
                2,
                1);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.InventoryFull));
        }

        [Test]
        public void Receive_Overflow_IsRejected()
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateReceive(
                true,
                int.MaxValue,
                1,
                2,
                1);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.Overflow));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Receive_NonPositiveAmount_IsRejected(int requestedAmount)
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateReceive(
                false,
                0,
                0,
                2,
                requestedAmount);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.InvalidAmount));
        }

        [TestCase(0, 0)]
        [TestCase(-1, 0)]
        [TestCase(2, 3)]
        public void Receive_InvalidCapacityState_IsContainerUnavailable(
            int slotCapacity,
            int occupiedSlotCount)
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateReceive(
                false,
                0,
                occupiedSlotCount,
                slotCapacity,
                1);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.ContainerUnavailable));
        }

        [Test]
        public void Receive_NonPositiveStoredStack_IsContainerUnavailable()
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateReceive(
                true,
                0,
                1,
                2,
                1);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.ContainerUnavailable));
        }

        [Test]
        public void Extraction_PartialAmount_IsAccepted()
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateExtraction(
                true,
                5,
                2);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(LootInventoryRules.CalculateRemainingAmount(5, 2), Is.EqualTo(3));
        }

        [Test]
        public void Extraction_CompleteAmount_IsAccepted()
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateExtraction(
                true,
                5,
                5);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(LootInventoryRules.CalculateRemainingAmount(5, 5), Is.Zero);
        }

        [Test]
        public void Extraction_MoreThanStored_IsInsufficient()
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateExtraction(
                true,
                5,
                6);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.InsufficientAmount));
        }

        [Test]
        public void Extraction_MissingStack_IsInsufficient()
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateExtraction(
                false,
                0,
                1);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.InsufficientAmount));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Extraction_NonPositiveAmount_IsRejected(int requestedAmount)
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateExtraction(
                true,
                5,
                requestedAmount);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.InvalidAmount));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Extraction_NonPositiveStoredStack_IsContainerUnavailable(int currentAmount)
        {
            LootTransferFailureReason result = LootInventoryRules.ValidateExtraction(
                true,
                currentAmount,
                1);

            Assert.That(result, Is.EqualTo(LootTransferFailureReason.ContainerUnavailable));
        }
    }
}
