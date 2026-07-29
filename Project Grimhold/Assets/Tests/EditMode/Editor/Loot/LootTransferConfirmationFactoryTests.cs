using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class LootTransferConfirmationFactoryTests
    {
        [Test]
        public void InvalidLootRejection_DoesNotRequireResolvableDefinition()
        {
            var expected = new LootTransferRequestIdentity(
                3,
                new EntityId(10),
                new EntityId(20),
                99,
                LootTransferQuantityMode.FullStack);

            bool valid = LootTransferConfirmation.TryReconstruct(
                3,
                10,
                20,
                99,
                0,
                false,
                (int)LootTransferFailureReason.InvalidLoot,
                50,
                expected,
                new EntityId(20),
                null,
                out LootTransferConfirmation confirmation,
                out string error);

            Assert.That(valid, Is.True, error);
            Assert.That(confirmation.Result.FailureReason, Is.EqualTo(LootTransferFailureReason.InvalidLoot));
            Assert.That(confirmation.ResolvedLootId, Is.Null);
        }

        [Test]
        public void UnresolvableSuccess_IsMalformed()
        {
            var expected = new LootTransferRequestIdentity(
                3,
                new EntityId(10),
                new EntityId(20),
                99,
                LootTransferQuantityMode.FullStack);

            bool valid = LootTransferConfirmation.TryReconstruct(
                3,
                10,
                20,
                99,
                2,
                true,
                (int)LootTransferFailureReason.None,
                50,
                expected,
                new EntityId(20),
                null,
                out _,
                out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("resolvable"));
        }

        [Test]
        public void MalformedMatchingConfirmation_CanBeReleasedBeforeReconstruction()
        {
            var client = new LootTransferClientRequestState();
            client.TryCreateCandidate(new EntityId(10), new EntityId(20), 4, LootTransferQuantityMode.SingleUnit, out LootTransferRequestIdentity expected);
            client.MarkSent(expected);

            Assert.That(client.TryRelease(expected.RequestSequence, out LootTransferRequestIdentity released), Is.True);
            bool valid = LootTransferConfirmation.TryReconstruct(
                released.RequestSequence,
                released.SourceId.Value,
                20,
                released.CatalogIndex,
                -1,
                true,
                (int)LootTransferFailureReason.None,
                50,
                released,
                new EntityId(20),
                null,
                out _,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(client.HasInFlight, Is.False);
        }

        [Test]
        public void UnknownSequence_DoesNotReleaseExpectedRequest()
        {
            var client = new LootTransferClientRequestState();
            client.TryCreateCandidate(new EntityId(10), new EntityId(20), 4, LootTransferQuantityMode.FullStack, out LootTransferRequestIdentity expected);
            client.MarkSent(expected);

            Assert.That(client.TryRelease(expected.RequestSequence + 1, out _), Is.False);
            Assert.That(client.HasInFlight, Is.True);
        }
    }
}
