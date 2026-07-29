using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class LootTransferRequestStateTests
    {
        [Test]
        public void Pending_IsNeverOverwritten()
        {
            var state = new LootTransferRequestState();
            LootTransferRequestIdentity first = Identity(1, 10, 2);
            LootTransferRequestIdentity second = Identity(2, 20, 3);

            Assert.That(state.TryEnqueue(first, out _), Is.EqualTo(LootTransferRequestState.Disposition.AcceptedPending));
            Assert.That(state.TryEnqueue(second, out _), Is.EqualTo(LootTransferRequestState.Disposition.BusyWithDifferentSequence));
            Assert.That(state.TryConsume(out LootTransferRequestIdentity consumed), Is.True);
            Assert.That(consumed, Is.EqualTo(first));
        }

        [Test]
        public void Pending_ExactDuplicateAndConflictingPayloadAreDistinguished()
        {
            var state = new LootTransferRequestState();
            LootTransferRequestIdentity original = Identity(4, 10, 2);
            state.TryEnqueue(original, out _);

            Assert.That(state.TryEnqueue(original, out _), Is.EqualTo(LootTransferRequestState.Disposition.PendingDuplicate));
            Assert.That(
                state.TryEnqueue(Identity(4, 10, 3), out _),
                Is.EqualTo(LootTransferRequestState.Disposition.PendingPayloadConflict));
            Assert.That(
                state.TryEnqueue(
                    new LootTransferRequestIdentity(
                        4,
                        new EntityId(10),
                        new EntityId(21),
                        2,
                        LootTransferQuantityMode.FullStack),
                    out _),
                Is.EqualTo(LootTransferRequestState.Disposition.PendingPayloadConflict));
            Assert.That(
                state.TryEnqueue(
                    new LootTransferRequestIdentity(
                        4,
                        new EntityId(10),
                        new EntityId(20),
                        2,
                        LootTransferQuantityMode.SingleUnit),
                    out _),
                Is.EqualTo(LootTransferRequestState.Disposition.PendingPayloadConflict));
            Assert.That(state.TryConsume(out LootTransferRequestIdentity consumed), Is.True);
            Assert.That(consumed, Is.EqualTo(original));
        }

        [Test]
        public void Processed_ExactDuplicateReturnsOnlyCachedConfirmation()
        {
            var state = new LootTransferRequestState();
            LootTransferRequestIdentity identity = Identity(7, 10, 2);
            LootTransferConfirmation confirmation = Rejected(identity, LootTransferFailureReason.InvalidLoot);
            state.TryEnqueue(identity, out _);
            state.TryConsume(out _);
            state.RecordProcessed(identity, confirmation);

            LootTransferRequestState.Disposition disposition = state.TryEnqueue(identity, out LootTransferConfirmation cached);

            Assert.That(disposition, Is.EqualTo(LootTransferRequestState.Disposition.ProcessedDuplicate));
            Assert.That(cached.RequestSequence, Is.EqualTo(7));
            Assert.That(cached.Result.FailureReason, Is.EqualTo(LootTransferFailureReason.InvalidLoot));
            Assert.That(state.HasPending, Is.False);
        }

        [Test]
        public void Processed_ConflictAndStaleSequenceDoNotEnterPending()
        {
            var state = new LootTransferRequestState();
            LootTransferRequestIdentity identity = Identity(7, 10, 2);
            state.TryEnqueue(identity, out _);
            state.TryConsume(out _);
            LootTransferConfirmation confirmation = Rejected(identity, LootTransferFailureReason.InvalidLoot);
            state.RecordProcessed(identity, confirmation);

            Assert.That(
                state.TryEnqueue(Identity(7, 11, 2), out _),
                Is.EqualTo(LootTransferRequestState.Disposition.ProcessedPayloadConflict));
            Assert.That(
                state.TryEnqueue(Identity(6, 10, 2), out _),
                Is.EqualTo(LootTransferRequestState.Disposition.StaleSequence));
            Assert.That(state.HasPending, Is.False);
        }

        [Test]
        public void Reset_ClearsPendingAndProcessedCache()
        {
            var state = new LootTransferRequestState();
            LootTransferRequestIdentity identity = Identity(1, 10, 2);
            state.TryEnqueue(identity, out _);
            state.TryConsume(out _);
            state.RecordProcessed(identity, Rejected(identity, LootTransferFailureReason.InvalidLoot));

            state.Reset();

            Assert.That(state.HasPending, Is.False);
            Assert.That(state.HasProcessed, Is.False);
            Assert.That(state.TryEnqueue(identity, out _), Is.EqualTo(LootTransferRequestState.Disposition.AcceptedPending));
        }

        [Test]
        public void RejectedConfirmation_DoesNotRequireResolvedLootMetadata()
        {
            LootTransferRequestIdentity identity = Identity(3, 10, 99);
            LootTransferConfirmation confirmation = Rejected(identity, LootTransferFailureReason.InvalidLoot);

            Assert.That(confirmation.Result.IsValid, Is.True);
            Assert.That(confirmation.ResolvedLootId, Is.Null);
            Assert.That(confirmation.CatalogIndex, Is.EqualTo(99));
        }

        private static LootTransferRequestIdentity Identity(uint sequence, int sourceId, int catalogIndex) =>
            new(
                sequence,
                new EntityId(sourceId),
                new EntityId(20),
                catalogIndex,
                LootTransferQuantityMode.FullStack);

        private static LootTransferConfirmation Rejected(
            in LootTransferRequestIdentity identity,
            LootTransferFailureReason reason)
        {
            LootTransferResult result = LootTransferResult.Rejected(reason);
            return new LootTransferConfirmation(
                identity.RequestSequence,
                identity.SourceId,
                identity.DestinationId,
                identity.CatalogIndex,
                100,
                result,
                null);
        }
    }
}
