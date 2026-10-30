using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class LootDropRequestStateTests
    {
        [Test]
        public void ExactProcessedDuplicate_ReplaysCachedConfirmation()
        {
            var state = new LootDropRequestState();
            var identity = new LootDropRequestIdentity(
                1,
                3,
                LootTransferQuantityMode.FullStack);

            Assert.That(
                state.TryEnqueue(identity, out _),
                Is.EqualTo(LootDropRequestState.Disposition.AcceptedPending));
            Assert.That(state.TryConsume(out LootDropRequestIdentity consumed), Is.True);
            var confirmation = new LootDropConfirmation(
                1,
                3,
                10,
                LootDropResult.Succeeded(4),
                new LootId("coin"));
            state.RecordProcessed(consumed, confirmation);

            Assert.That(
                state.TryEnqueue(identity, out LootDropConfirmation replay),
                Is.EqualTo(LootDropRequestState.Disposition.ProcessedDuplicate));
            Assert.That(replay.Result.Success, Is.True);
            Assert.That(replay.Result.DroppedAmount, Is.EqualTo(4));
        }

        [Test]
        public void PendingRequest_CannotBeOverwritten()
        {
            var state = new LootDropRequestState();
            var first = new LootDropRequestIdentity(1, 2, LootTransferQuantityMode.SingleUnit);
            var conflictingPayload = new LootDropRequestIdentity(1, 3, LootTransferQuantityMode.SingleUnit);
            var concurrent = new LootDropRequestIdentity(2, 2, LootTransferQuantityMode.FullStack);

            Assert.That(
                state.TryEnqueue(first, out _),
                Is.EqualTo(LootDropRequestState.Disposition.AcceptedPending));
            Assert.That(
                state.TryEnqueue(conflictingPayload, out _),
                Is.EqualTo(LootDropRequestState.Disposition.PendingPayloadConflict));
            Assert.That(
                state.TryEnqueue(concurrent, out _),
                Is.EqualTo(LootDropRequestState.Disposition.BusyWithDifferentSequence));
            Assert.That(state.TryConsume(out LootDropRequestIdentity consumed), Is.True);
            Assert.That(consumed, Is.EqualTo(first));
        }

        [TestCase(8, 19)]
        [TestCase(7, 20)]
        public void AddressAndRevision_AffectDropEqualityHashAndReplayConflicts(int changedSlot, int changedRevision)
        {
            var original = new LootDropRequestIdentity(5, 2, LootTransferQuantityMode.FullStack, 7, 19);
            var duplicate = new LootDropRequestIdentity(5, 2, LootTransferQuantityMode.FullStack, 7, 19);
            var conflict = new LootDropRequestIdentity(5, 2, LootTransferQuantityMode.FullStack, changedSlot, changedRevision);
            Assert.That(duplicate, Is.EqualTo(original));
            Assert.That(duplicate.GetHashCode(), Is.EqualTo(original.GetHashCode()));
            Assert.That(conflict, Is.Not.EqualTo(original));
            Assert.That(conflict.GetHashCode(), Is.Not.EqualTo(original.GetHashCode()));
            var state = new LootDropRequestState();
            state.TryEnqueue(original, out _);
            Assert.That(state.TryEnqueue(duplicate, out _), Is.EqualTo(LootDropRequestState.Disposition.PendingDuplicate));
            Assert.That(state.TryEnqueue(conflict, out _), Is.EqualTo(LootDropRequestState.Disposition.PendingPayloadConflict));
            Assert.That(state.TryConsume(out LootDropRequestIdentity consumed), Is.True);
            Assert.That(consumed, Is.EqualTo(original));
            state.RecordProcessed(consumed, new LootDropConfirmation(5, 2, 20,
                LootDropResult.Rejected(LootDropFailureReason.PlayerUnavailable), null));
            Assert.That(state.TryEnqueue(conflict, out _), Is.EqualTo(LootDropRequestState.Disposition.ProcessedPayloadConflict));
            Assert.That(state.TryEnqueue(duplicate, out LootDropConfirmation replay), Is.EqualTo(LootDropRequestState.Disposition.ProcessedDuplicate));
            Assert.That(replay.Result.FailureReason, Is.EqualTo(LootDropFailureReason.PlayerUnavailable));
            Assert.That(state.TryConsume(out _), Is.False);
        }

        [Test]
        public void AddressedDropClient_RetainsExactIntentAndDoesNotRetargetAfterRelease()
        {
            var state = new LootDropClientRequestState();
            Assert.That(state.TryCreateCandidate(2, LootTransferQuantityMode.FullStack, 7, 19,
                out LootDropRequestIdentity original), Is.True);
            Assert.That(original.SourceInventorySlotIndex, Is.EqualTo(7));
            Assert.That(original.ExpectedInventoryRevision, Is.EqualTo(19));
            state.MarkSent(original);
            Assert.That(state.TryCreateCandidate(2, LootTransferQuantityMode.FullStack, 8, 20, out _), Is.False);
            Assert.That(state.TryRelease(original.RequestSequence, out LootDropRequestIdentity released), Is.True);
            Assert.That(released, Is.EqualTo(original));
            Assert.That(state.HasInFlight, Is.False);
            Assert.That(state.TryCreateCandidate(2, LootTransferQuantityMode.FullStack, 8, 20,
                out LootDropRequestIdentity next), Is.True);
            Assert.That(next.RequestSequence, Is.EqualTo(original.RequestSequence + 1));
        }

        [TestCase(-1)]
        [TestCase(30)]
        public void AddressedDropClient_RejectsInvalidAddressWithoutConsumingSequence(int slotIndex)
        {
            var state = new LootDropClientRequestState();
            Assert.That(state.TryCreateCandidate(2, LootTransferQuantityMode.FullStack, slotIndex, 19, out _), Is.False);
            Assert.That(state.TryCreateCandidate(2, LootTransferQuantityMode.FullStack, 29, 19,
                out LootDropRequestIdentity valid), Is.True);
            Assert.That(valid.RequestSequence, Is.EqualTo(1));
        }

        [Test]
        public void LegacyDropIdentityAndClientRemainUnaddressed()
        {
            var legacy = new LootDropRequestIdentity(5, 2, LootTransferQuantityMode.FullStack);
            Assert.That(legacy.SourceInventorySlotIndex, Is.EqualTo(-1));
            Assert.That(legacy.ExpectedInventoryRevision, Is.EqualTo(-1));
            var state = new LootDropClientRequestState();
            Assert.That(state.TryCreateCandidate(2, LootTransferQuantityMode.FullStack, out LootDropRequestIdentity candidate), Is.True);
            Assert.That(candidate.SourceInventorySlotIndex, Is.EqualTo(-1));
            Assert.That(candidate.ExpectedInventoryRevision, Is.EqualTo(-1));
        }

        [Test]
        public void ProcessedRequest_RejectsPayloadConflictAndOlderSequence()
        {
            var state = new LootDropRequestState();
            var processed = new LootDropRequestIdentity(
                5,
                2,
                LootTransferQuantityMode.SingleUnit);
            Assert.That(
                state.TryEnqueue(processed, out _),
                Is.EqualTo(LootDropRequestState.Disposition.AcceptedPending));
            Assert.That(state.TryConsume(out LootDropRequestIdentity consumed), Is.True);
            state.RecordProcessed(
                consumed,
                new LootDropConfirmation(
                    5,
                    2,
                    20,
                    LootDropResult.Succeeded(1),
                    new LootId("coin")));

            var conflict = new LootDropRequestIdentity(
                5,
                3,
                LootTransferQuantityMode.SingleUnit);
            var stale = new LootDropRequestIdentity(
                4,
                2,
                LootTransferQuantityMode.SingleUnit);

            Assert.That(
                state.TryEnqueue(conflict, out _),
                Is.EqualTo(LootDropRequestState.Disposition.ProcessedPayloadConflict));
            Assert.That(
                state.TryEnqueue(stale, out _),
                Is.EqualTo(LootDropRequestState.Disposition.StaleSequence));
            Assert.That(state.TryConsume(out _), Is.False);
        }
    }
}
