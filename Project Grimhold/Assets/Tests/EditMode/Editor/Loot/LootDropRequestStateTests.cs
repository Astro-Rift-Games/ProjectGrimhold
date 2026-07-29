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
