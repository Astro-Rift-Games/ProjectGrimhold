#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;

public sealed class RaidInventoryDisplayOrderTests
{
    private static LootEntry Entry(string id, int amount = 1) => new(new LootId(id), amount);

    [Test]
    public void Seed_Reconcile_PreservesSeedThenAppendsAndCompacts()
    {
        var order = new RaidInventoryDisplayOrder();
        order.Seed(new[] { Entry("b"), Entry("a") });
        order.Reconcile(new[] { Entry("a"), Entry("b"), Entry("c") });
        Assert.That(order.Entries.Select(e => e.LootId.Value), Is.EqualTo(new[] { "b", "a", "c" }));
        order.Reconcile(new[] { Entry("a", 9), Entry("c") });
        Assert.That(order.Entries.Select(e => e.LootId.Value), Is.EqualTo(new[] { "a", "c" }));
        Assert.That(order.Entries[0].Amount, Is.EqualTo(9));
    }

    [Test]
    public void Sort_IsExplicit_AmountsDoNotAffectOrder_ThenNewItemsAppendUnsorted()
    {
        var order = new RaidInventoryDisplayOrder();
        order.Reconcile(new[] { Entry("z", 99), Entry("b", 2) });
        order.Sort(null);
        Assert.That(order.Entries.Select(e => e.LootId.Value), Is.EqualTo(new[] { "b", "z" }));
        order.Reconcile(new[] { Entry("a"), Entry("z", 1), Entry("b", 20) });
        Assert.That(order.Entries.Select(e => e.LootId.Value), Is.EqualTo(new[] { "b", "z", "a" }));
        Assert.That(order.Entries.Select(e => e.Amount), Is.EqualTo(new[] { 20, 1, 1 }));
    }
}
#endif
