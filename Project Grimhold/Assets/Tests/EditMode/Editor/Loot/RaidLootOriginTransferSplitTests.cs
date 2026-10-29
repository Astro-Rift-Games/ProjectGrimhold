using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class RaidLootOriginTransferSplitTests
    {
        [Test]
        public void TrySplit_TakesCanonicalUnitsAndKeepsExactRemainder()
        {
            RaidLootOriginTransfer transfer = Mixed(2, (1, 3), (4, 5));

            Assert.That(transfer.TrySplit(4, out RaidLootOriginTransfer taken, out RaidLootOriginTransfer remainder), Is.True);

            Assert.That(taken.Buckets, Is.EqualTo(new[]
            {
                new RaidLootOriginBucket(RaidLootOrigin.Dungeon, 2),
                new RaidLootOriginBucket(PlayerOrigin(1), 2)
            }));
            Assert.That(remainder.Buckets, Is.EqualTo(new[]
            {
                new RaidLootOriginBucket(PlayerOrigin(1), 1),
                new RaidLootOriginBucket(PlayerOrigin(4), 5)
            }));
            Assert.That(taken.TryGetTotal(out int takenTotal) && takenTotal == 4, Is.True);
            Assert.That(remainder.TryGetTotal(out int remainderTotal) && remainderTotal == 6, Is.True);
        }

        [Test]
        public void TrySplit_OnBucketBoundaryDoesNotEmitEmptyBuckets()
        {
            RaidLootOriginTransfer transfer = Mixed(3, (2, 4));

            Assert.That(transfer.TrySplit(3, out RaidLootOriginTransfer taken, out RaidLootOriginTransfer remainder), Is.True);

            Assert.That(taken.Buckets, Is.EqualTo(new[] { new RaidLootOriginBucket(RaidLootOrigin.Dungeon, 3) }));
            Assert.That(remainder.Buckets, Is.EqualTo(new[] { new RaidLootOriginBucket(PlayerOrigin(2), 4) }));
        }

        [Test]
        public void TrySplit_FullAmountLeavesEmptyRemainder()
        {
            RaidLootOriginTransfer transfer = Mixed(3, (2, 4));

            Assert.That(transfer.TrySplit(7, out RaidLootOriginTransfer taken, out RaidLootOriginTransfer remainder), Is.True);

            Assert.That(taken.Buckets, Is.EqualTo(transfer.Buckets));
            Assert.That(remainder.Count, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(8)]
        public void TrySplit_RejectsAmountOutsideTotal(int amount)
        {
            RaidLootOriginTransfer transfer = Mixed(3, (2, 4));

            Assert.That(transfer.TrySplit(amount, out RaidLootOriginTransfer taken, out RaidLootOriginTransfer remainder), Is.False);
            Assert.That(taken, Is.Null);
            Assert.That(remainder, Is.Null);
        }

        [Test]
        public void TrySplit_RejectsNonCanonicalTransfer()
        {
            var transfer = new RaidLootOriginTransfer(new[]
            {
                new RaidLootOriginBucket(PlayerOrigin(2), 1),
                new RaidLootOriginBucket(RaidLootOrigin.Dungeon, 1)
            });

            Assert.That(transfer.TrySplit(1, out _, out _), Is.False);
        }

        private static RaidLootOriginTransfer Mixed(int dungeonAmount, params (int participant, int amount)[] players)
        {
            var buckets = new RaidLootOriginBucket[players.Length + 1];
            buckets[0] = new RaidLootOriginBucket(RaidLootOrigin.Dungeon, dungeonAmount);
            for (int index = 0; index < players.Length; index++)
            {
                buckets[index + 1] = new RaidLootOriginBucket(PlayerOrigin(players[index].participant), players[index].amount);
            }

            return new RaidLootOriginTransfer(buckets);
        }

        private static RaidLootOrigin PlayerOrigin(int value)
        {
            Assert.That(RaidParticipantId.TryCreate(value, out RaidParticipantId participant), Is.True);
            Assert.That(RaidLootOrigin.TryCreatePlayer(participant, out RaidLootOrigin origin), Is.True);
            return origin;
        }
    }
}
