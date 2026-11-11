#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

[Category("TASK214")]
public sealed class RaidLootOriginSelectionOrderTests
{
    private const int CatalogIndex = 0;

    [Test]
    public void PlayerFirst_SpendsPlayerOriginBeforeDungeon_AndKeepsCanonicalBucketOrder()
    {
        RaidLootOriginPackedState state = CreateState(dungeonAmount: 2, playerAmount: 3);

        bool resolved = RaidLootOriginIndexedStateUtility.TryResolveTransfer(
            state, CatalogIndex, 4, out RaidLootOriginTransfer transfer,
            RaidLootOriginSelectionOrder.PlayerFirst);

        Assert.That(resolved, Is.True);
        Assert.That(transfer.TryGetTotal(out int total), Is.True);
        Assert.That(total, Is.EqualTo(4));
        Assert.That(transfer.Count, Is.EqualTo(2));
        Assert.That(transfer.Buckets[0].Origin.IsDungeon, Is.True);
        Assert.That(transfer.Buckets[0].Amount, Is.EqualTo(1));
        Assert.That(transfer.Buckets[1].Origin.IsPlayer, Is.True);
        Assert.That(transfer.Buckets[1].Amount, Is.EqualTo(3));
    }

    [Test]
    public void PlayerFirst_SingleUnitComesFromPlayerOrigin()
    {
        RaidLootOriginPackedState state = CreateState(dungeonAmount: 2, playerAmount: 3);

        bool resolved = RaidLootOriginIndexedStateUtility.TryResolveTransfer(
            state, CatalogIndex, 1, out RaidLootOriginTransfer transfer,
            RaidLootOriginSelectionOrder.PlayerFirst);

        Assert.That(resolved, Is.True);
        Assert.That(transfer.Count, Is.EqualTo(1));
        Assert.That(transfer.Buckets[0].Origin.IsPlayer, Is.True);
    }

    [Test]
    public void Canonical_StillSpendsDungeonFirst()
    {
        RaidLootOriginPackedState state = CreateState(dungeonAmount: 2, playerAmount: 3);

        bool resolved = RaidLootOriginIndexedStateUtility.TryResolveTransfer(
            state, CatalogIndex, 1, out RaidLootOriginTransfer transfer);

        Assert.That(resolved, Is.True);
        Assert.That(transfer.Count, Is.EqualTo(1));
        Assert.That(transfer.Buckets[0].Origin.IsDungeon, Is.True);
    }

    [Test]
    public void PlayerFirst_ExceedingTotalIsRejected()
    {
        RaidLootOriginPackedState state = CreateState(dungeonAmount: 2, playerAmount: 3);

        bool resolved = RaidLootOriginIndexedStateUtility.TryResolveTransfer(
            state, CatalogIndex, 6, out _, RaidLootOriginSelectionOrder.PlayerFirst);

        Assert.That(resolved, Is.False);
    }

    private static RaidLootOriginPackedState CreateState(int dungeonAmount, int playerAmount)
    {
        Assert.That(RaidParticipantId.TryCreate(1, out RaidParticipantId participant), Is.True);
        Assert.That(RaidLootOrigin.TryCreatePlayer(participant, out RaidLootOrigin player), Is.True);

        RaidLootOriginPackedState state = default;
        Assert.That(RaidLootOriginIndexedStateUtility.TryAdd(
            ref state,
            CatalogIndex,
            new RaidLootOriginTransfer(new[]
            {
                new RaidLootOriginBucket(RaidLootOrigin.Dungeon, dungeonAmount),
                new RaidLootOriginBucket(player, playerAmount)
            })), Is.True);
        return state;
    }
}
#endif
