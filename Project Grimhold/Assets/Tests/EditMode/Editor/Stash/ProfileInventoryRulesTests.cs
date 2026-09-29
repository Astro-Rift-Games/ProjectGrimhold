using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public class ProfileInventoryRulesTests
{
    private static readonly LootId Potion = new LootId("healthpotion");
    private static readonly LootId Bone = new LootId("bone");

    [Test]
    public void TryMerge_StacksExistingLootAndAppendsNewLoot()
    {
        var items = new List<StashItem> { new StashItem(Potion, 2) };

        Assert.That(ProfileInventoryRules.TryMerge(items, new[] { new StashItem(Potion, 3), new StashItem(Bone, 1) }), Is.True);

        Assert.That(items, Is.EqualTo(new[] { new StashItem(Potion, 5), new StashItem(Bone, 1) }));
    }

    [Test]
    public void TryMerge_RejectsStackOverflow()
    {
        var items = new List<StashItem> { new StashItem(Potion, int.MaxValue) };

        Assert.That(ProfileInventoryRules.TryMerge(items, new[] { new StashItem(Potion, 1) }), Is.False);
    }

    [Test]
    public void TryRemove_FreesTheSlotOnlyWhenNoUnitsRemain()
    {
        var items = new List<StashItem> { new StashItem(Potion, 3), new StashItem(Bone, 1) };

        Assert.That(ProfileInventoryRules.TryRemove(items, Potion, 2), Is.True);
        Assert.That(ProfileInventoryRules.TryRemove(items, Bone, 1), Is.True);
        Assert.That(ProfileInventoryRules.TryRemove(items, Potion, 2), Is.False);

        Assert.That(items, Is.EqualTo(new[] { new StashItem(Potion, 1) }));
    }

    [Test]
    public void ExceedsCapacity_CountsOnlyNewSlots()
    {
        var items = new List<StashItem> { new StashItem(Potion, 1) };

        Assert.That(ProfileInventoryRules.ExceedsCapacity(items, new[] { new StashItem(Potion, 99) }, 1), Is.False);
        Assert.That(ProfileInventoryRules.ExceedsCapacity(items, new[] { new StashItem(Bone, 1) }, 1), Is.True);
        Assert.That(ProfileInventoryRules.ExceedsCapacity(items, new[] { new StashItem(Bone, 1) }, 2), Is.False);
    }
}
