using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class LootTransferQuantityResolverTests
    {
        [TestCase(1)]
        [TestCase(8)]
        public void SingleUnit_AlwaysResolvesExactlyOne(int availableAmount)
        {
            LootTransferFailureReason failure = LootTransferQuantityResolver.Resolve(
                LootTransferQuantityMode.SingleUnit,
                availableAmount,
                out int requestedAmount);

            Assert.That(failure, Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(requestedAmount, Is.EqualTo(1));
        }

        [Test]
        public void FullStack_UsesCompleteAuthoritativeAmount()
        {
            LootTransferFailureReason failure = LootTransferQuantityResolver.Resolve(
                LootTransferQuantityMode.FullStack,
                8,
                out int requestedAmount);

            Assert.That(failure, Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(requestedAmount, Is.EqualTo(8));
        }

        [TestCase(LootTransferQuantityMode.SingleUnit, 0)]
        [TestCase(LootTransferQuantityMode.FullStack, 0)]
        [TestCase(LootTransferQuantityMode.SingleUnit, -1)]
        public void MissingAmount_IsRejectedWithoutRequestedQuantity(
            LootTransferQuantityMode quantityMode,
            int availableAmount)
        {
            LootTransferFailureReason failure = LootTransferQuantityResolver.Resolve(
                quantityMode,
                availableAmount,
                out int requestedAmount);

            Assert.That(failure, Is.EqualTo(LootTransferFailureReason.InsufficientAmount));
            Assert.That(requestedAmount, Is.Zero);
        }

        [Test]
        public void UnsupportedMode_IsRejectedBeforeReadingAmount()
        {
            LootTransferFailureReason failure = LootTransferQuantityResolver.Resolve(
                default,
                8,
                out int requestedAmount);

            Assert.That(failure, Is.EqualTo(LootTransferFailureReason.InvalidAmount));
            Assert.That(requestedAmount, Is.Zero);
        }
    }
}
