using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class LootInventoryRulesTests
    {
        [TestCase(1, true)]
        [TestCase(16, true)]
        [TestCase(17, false)]
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
