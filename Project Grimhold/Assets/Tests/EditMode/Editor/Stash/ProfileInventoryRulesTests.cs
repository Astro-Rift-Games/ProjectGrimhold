using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public class ProfileInventoryRulesTests
{
    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Bone = new LootId("bone");

    [Test]
    public void TryMerge_StacksExistingLootAndPlacesNewLootInLowestFreeSlot()
    {
        var items = new List<StashItem> { new StashItem(Potion, 2, 0) };
        Assert.That(ProfileInventoryRules.TryMerge(items, new[] { new StashItem(Potion, 3), new StashItem(Bone, 1) }), Is.True);
        Assert.That(items, Is.EqualTo(new[] { new StashItem(Potion, 5, 0), new StashItem(Bone, 1, 1) }));
    }

    [Test]
    public void TryMerge_RejectsStackOverflow()
    {
        var items = new List<StashItem> { new StashItem(Potion, int.MaxValue, 0) };
        Assert.That(ProfileInventoryRules.TryMerge(items, new[] { new StashItem(Potion, 1) }), Is.False);
        Assert.That(items, Is.EqualTo(new[] { new StashItem(Potion, int.MaxValue, 0) }));
    }

    [Test]
    public void TryRemove_FreesTheSlotOnlyWhenNoUnitsRemain()
    {
        var items = new List<StashItem> { new StashItem(Potion, 3, 0), new StashItem(Bone, 1, 1) };
        Assert.That(ProfileInventoryRules.TryRemove(items, Potion, 2), Is.True);
        Assert.That(ProfileInventoryRules.TryRemove(items, Bone, 1), Is.True);
        Assert.That(ProfileInventoryRules.TryRemove(items, Potion, 2), Is.False);
        Assert.That(items, Is.EqualTo(new[] { new StashItem(Potion, 1, 0) }));
    }

    [Test]
    public void ExceedsCapacity_CountsOnlyNewSlots()
    {
        var items = new List<StashItem> { new StashItem(Potion, 1, 0) };
        Assert.That(ProfileInventoryRules.ExceedsCapacity(items, new[] { new StashItem(Potion, 99) }, 1), Is.False);
        Assert.That(ProfileInventoryRules.ExceedsCapacity(items, new[] { new StashItem(Bone, 1) }, 1), Is.True);
        Assert.That(ProfileInventoryRules.ExceedsCapacity(items, new[] { new StashItem(Bone, 1) }, 2), Is.False);
    }

    [Test]
    public void TryMerge_WhenLootHasDifferentOriginOrExpeditionId_DoesNotMergeAndAllocatesNewSlot()
    {
        var items = new List<StashItem>
        {
            new StashItem(Potion, 2, 0, null, "exp-1", "Dungeon")
        };

        var incoming = new[]
        {
            new StashItem(Potion, 3, -1, null, "exp-1", "Dungeon"), // Compatible -> merge into slot 0
            new StashItem(Potion, 1, -1, null, "exp-2", "Dungeon"), // Different expedition -> separate slot 1
            new StashItem(Potion, 4, -1, null, "exp-1", "Town")     // Different origin -> separate slot 2
        };

        Assert.That(ProfileInventoryRules.TryMerge(items, incoming), Is.True);
        Assert.That(items.Count, Is.EqualTo(3));
        Assert.That(items[0], Is.EqualTo(new StashItem(Potion, 5, 0, null, "exp-1", "Dungeon")));
        Assert.That(items[1], Is.EqualTo(new StashItem(Potion, 1, 1, null, "exp-2", "Dungeon")));
        Assert.That(items[2], Is.EqualTo(new StashItem(Potion, 4, 2, null, "exp-1", "Town")));
    }

    [Test]
    public void TryMove_WhenTargetHasSameLootIdButDifferentOriginOrExpeditionId_RejectsMergeAndSwapsSlots()
    {
        var items = new List<StashItem>
        {
            new StashItem(Potion, 2, 0, null, "exp-1", "Dungeon"),
            new StashItem(Potion, 3, 1, null, "exp-2", "Dungeon")
        };

        // Attempting to move slot 0 onto slot 1: different expedition must swap, never merge!
        bool result = ProfileInventoryRules.TryMove(items, 0, items, 1, Potion, 10, 10);
        Assert.That(result, Is.True);
        Assert.That(items.Count, Is.EqualTo(2), "Stacks with different ExpeditionId must never merge.");
        Assert.That(items[0], Is.EqualTo(new StashItem(Potion, 3, 0, null, "exp-2", "Dungeon")));
        Assert.That(items[1], Is.EqualTo(new StashItem(Potion, 2, 1, null, "exp-1", "Dungeon")));
    }

    [Test]
    public void ExceedsCapacity_WhenSameLootIdHasDifferentOriginOrExpeditionId_RequiresNewSlot()
    {
        var items = new List<StashItem>
        {
            new StashItem(Potion, 1, 0, null, "exp-1", "Dungeon")
        };

        // Same lootId and same expedition & origin -> fits in slot 0 (capacity 1)
        Assert.That(ProfileInventoryRules.ExceedsCapacity(items, new[] { new StashItem(Potion, 5, -1, null, "exp-1", "Dungeon") }, 1), Is.False);

        // Same lootId but different expedition -> requires new slot -> exceeds capacity 1!
        Assert.That(ProfileInventoryRules.ExceedsCapacity(items, new[] { new StashItem(Potion, 1, -1, null, "exp-2", "Dungeon") }, 1), Is.True);
    }
}
