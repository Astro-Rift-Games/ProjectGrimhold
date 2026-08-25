using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class PlayerExpeditionLootSnapshotTests
    {
        [Test]
        public void OriginTotals_AcceptMixedBucketsMatchingLootQuantity()
        {
            LootId lootId = new LootId("coin");
            Assert.That(RaidParticipantId.TryCreate(1, out RaidParticipantId participantId), Is.True);
            Assert.That(RaidLootOrigin.TryCreatePlayer(participantId, out RaidLootOrigin player), Is.True);

            Assert.That(
                PlayerExpeditionLootSnapshot.TryValidateOriginTotals(
                    new[] { new LootEntry(lootId, 5) },
                    new[]
                    {
                        new RaidLootOriginEntry(lootId, RaidLootOrigin.Dungeon, 2),
                        new RaidLootOriginEntry(lootId, player, 3)
                    },
                    out string error),
                Is.True,
                error);
        }

        [Test]
        public void OriginTotals_RejectMismatchDuplicateAndUnexpectedLoot()
        {
            LootId lootId = new LootId("coin");
            LootEntry[] loot = { new LootEntry(lootId, 2) };

            Assert.That(PlayerExpeditionLootSnapshot.TryValidateOriginTotals(
                loot,
                new[] { new RaidLootOriginEntry(lootId, RaidLootOrigin.Dungeon, 1) },
                out _), Is.False);
            Assert.That(PlayerExpeditionLootSnapshot.TryValidateOriginTotals(
                loot,
                new[]
                {
                    new RaidLootOriginEntry(lootId, RaidLootOrigin.Dungeon, 1),
                    new RaidLootOriginEntry(lootId, RaidLootOrigin.Dungeon, 1)
                },
                out _), Is.False);
            Assert.That(PlayerExpeditionLootSnapshot.TryValidateOriginTotals(
                loot,
                new[] { new RaidLootOriginEntry(new LootId("gem"), RaidLootOrigin.Dungeon, 2) },
                out _), Is.False);
        }
    }
}
