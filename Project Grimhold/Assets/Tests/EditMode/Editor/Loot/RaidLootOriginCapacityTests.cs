using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class RaidLootOriginCapacityTests
    {
        [Test]
        public void CompactEndpoint_HoldsSixteenLootIdsWithAllSeventeenOrigins()
        {
            var state = new RaidLootOriginPackedState();
            IReadOnlyList<RaidLootOriginBucket> allOrigins = CreateAllOrigins();

            for (int catalogIndex = 0; catalogIndex < RaidLootOriginPackedBuffer.MaximumStacks; catalogIndex++)
            {
                Assert.That(
                    RaidLootOriginIndexedStateUtility.TryAdd(
                        ref state,
                        catalogIndex,
                        new RaidLootOriginTransfer(allOrigins)),
                    Is.True,
                    $"catalog index {catalogIndex}");
            }

            Assert.That(state.BucketCount, Is.EqualTo(272));
            Assert.That(RaidLootOriginIndexedStateUtility.TryValidateState(state), Is.True);
            for (int catalogIndex = 0; catalogIndex < RaidLootOriginPackedBuffer.MaximumStacks; catalogIndex++)
            {
                Assert.That(
                    RaidLootOriginIndexedStateUtility.TryResolveTransfer(
                        state,
                        catalogIndex,
                        RaidLootOriginPackedBuffer.OriginsPerLoot,
                        out RaidLootOriginTransfer transfer),
                    Is.True);
                Assert.That(transfer.Count, Is.EqualTo(17));
                Assert.That(transfer.Buckets[0].Origin.IsDungeon, Is.True);
            }
        }

        [Test]
        public void CompactEndpoint_RejectsInvalidParticipantWithoutMutatingState()
        {
            var state = new RaidLootOriginPackedState();
            IReadOnlyList<RaidLootOriginBucket> allOrigins = CreateAllOrigins();
            for (int catalogIndex = 0; catalogIndex < RaidLootOriginPackedBuffer.MaximumStacks; catalogIndex++)
            {
                Assert.That(RaidLootOriginIndexedStateUtility.TryAdd(
                    ref state, catalogIndex, new RaidLootOriginTransfer(allOrigins)), Is.True);
            }

            int bucketsBefore = state.BucketCount;
            Assert.That(RaidLootOrigin.TryCreatePlayer(default, out _), Is.False);
            Assert.That(state.BucketCount, Is.EqualTo(bucketsBefore));
        }

        [Test]
        public void PickupCompactState_RoundTripsDungeonAndSixteenPlayers()
        {
            var transfer = new RaidLootOriginTransfer(CreateAllOrigins());
            Assert.That(RaidLootPickupOriginStateCodec.TryEncode(
                transfer, 17, out RaidLootPickupCompactOriginState state), Is.True);
            Assert.That(RaidLootPickupOriginStateCodec.TryDecode(state, 17, out RaidLootOriginTransfer decoded), Is.True);
            Assert.That(decoded.Count, Is.EqualTo(17));
            for (int index = 0; index < transfer.Count; index++)
            {
                Assert.That(decoded.Buckets[index].Origin, Is.EqualTo(transfer.Buckets[index].Origin));
                Assert.That(decoded.Buckets[index].Amount, Is.EqualTo(1));
            }
        }

        [Test]
        public void RaidDistinctLootCapacity_AcceptsSixteenAndRejectsSeventeen()
        {
            Assert.That(LootInventoryRules.IsValidSlotCapacity(16, PlayerLootReceiver.MaxDistinctLootTypes), Is.True);
            Assert.That(LootInventoryRules.IsValidSlotCapacity(17, PlayerLootReceiver.MaxDistinctLootTypes), Is.False);
        }

        [Test]
        public void Transfer_ResolvesDungeonThenParticipantIdAndDestinationKeepsLogicalIds()
        {
            Assert.That(RaidParticipantId.TryCreate(16, out RaidParticipantId highId), Is.True);
            Assert.That(RaidParticipantId.TryCreate(1, out RaidParticipantId lowId), Is.True);
            Assert.That(RaidLootOrigin.TryCreatePlayer(highId, out RaidLootOrigin high), Is.True);
            Assert.That(RaidLootOrigin.TryCreatePlayer(lowId, out RaidLootOrigin low), Is.True);
            Assert.That(RaidLootOriginTransfer.TryCreate(high, 1, out RaidLootOriginTransfer highTransfer), Is.True);
            Assert.That(RaidLootOriginTransfer.TryCreate(low, 1, out RaidLootOriginTransfer lowTransfer), Is.True);

            var source = new RaidLootOriginPackedState();
            Assert.That(RaidLootOriginIndexedStateUtility.TryAdd(
                ref source, 3, highTransfer), Is.True);
            Assert.That(RaidLootOriginIndexedStateUtility.TryAdd(
                ref source, 3, RaidLootOriginTransfer.Dungeon(1)), Is.True);
            Assert.That(RaidLootOriginIndexedStateUtility.TryAdd(
                ref source, 3, lowTransfer), Is.True);

            Assert.That(RaidLootOriginIndexedStateUtility.TryResolveTransfer(
                source, 3, 3, out RaidLootOriginTransfer payload), Is.True);
            Assert.That(payload.Buckets[0].Origin, Is.EqualTo(RaidLootOrigin.Dungeon));
            Assert.That(payload.Buckets[1].Origin, Is.EqualTo(low));
            Assert.That(payload.Buckets[2].Origin, Is.EqualTo(high));

            var destination = new RaidLootOriginPackedState();
            Assert.That(RaidLootOriginIndexedStateUtility.TryAdd(
                ref destination, 9, lowTransfer), Is.True);
            Assert.That(RaidLootOriginIndexedStateUtility.TryAdd(
                ref destination, 3, payload), Is.True);
            Assert.That(RaidLootOriginIndexedStateUtility.TryResolveTransfer(
                destination, 3, 3, out RaidLootOriginTransfer received), Is.True);
            CollectionAssert.AreEqual(payload.Buckets, received.Buckets);
        }

        [Test]
        public void PackedStateValueCopy_PreservesParticipantIdsAndBucketsWithoutFixup()
        {
            var original = new RaidLootOriginPackedState();
            IReadOnlyList<RaidLootOriginBucket> origins = CreateAllOrigins();
            Assert.That(RaidLootOriginIndexedStateUtility.TryAdd(
                ref original, 63, new RaidLootOriginTransfer(origins)), Is.True);

            RaidLootOriginPackedState restored = original;
            Assert.That(restored.BucketCount, Is.EqualTo(original.BucketCount));
            Assert.That(RaidLootOriginIndexedStateUtility.TryResolveTransfer(
                restored, 63, 17, out RaidLootOriginTransfer transfer), Is.True);
            CollectionAssert.AreEqual(origins, transfer.Buckets);
        }

        private static IReadOnlyList<RaidLootOriginBucket> CreateAllOrigins()
        {
            var buckets = new List<RaidLootOriginBucket>(17)
            {
                new(RaidLootOrigin.Dungeon, 1)
            };
            for (int index = 0; index < RaidLootOriginPackedBuffer.MaximumPlayerOrigins; index++)
            {
                Assert.That(RaidParticipantId.TryCreate(index + 1, out RaidParticipantId participantId), Is.True);
                Assert.That(RaidLootOrigin.TryCreatePlayer(participantId, out RaidLootOrigin origin), Is.True);
                buckets.Add(new RaidLootOriginBucket(origin, 1));
            }
            return buckets.AsReadOnly();
        }
    }
}
