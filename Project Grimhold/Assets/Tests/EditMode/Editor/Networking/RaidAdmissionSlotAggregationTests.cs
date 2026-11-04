#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;

public sealed class RaidAdmissionSlotAggregationTests
{
    [Test]
    public void DuplicateSlotReservation_CreatesOneAggregateAdmissionEntry()
    {
        RaidCode.TryParse("123456", out var code);
        var bone = new LootId("bone");
        var sword = new LootId("rapier");
        var reservation = new PendingLoadoutReservation("reservation", new[]
        {
            new StashItem(bone, 3, 7), new StashItem(bone, 2, 1)
        }, new PreparedEquipmentLoadout(sword, default));
        Assert.That(RaidAdmissionData.TryCreate(code, new ProfileId("49494949494949494949494949494949"),
            reservation, ProgressionBalanceDefaults.InitialCharacterAttributeState, 1, 0, 0, out var data), Is.True);
        Assert.That(data.ReservedLoadout.Count, Is.EqualTo(2));
        Assert.That(data.ReservedLoadout[0].LootId, Is.EqualTo(bone));
        Assert.That(data.ReservedLoadout[0].Amount, Is.EqualTo(5));
        Assert.That(reservation.Items.Count, Is.EqualTo(2), "Admission must not merge the durable reservation.");
    }

    [TestCase(5, true)]
    [TestCase(4, false)]
    public void ConfirmationComparison_AggregatesSplitStacks_RejectsRealQuantityMismatch(int actual, bool matches)
    {
        var bone = new LootId("bone");
        var method = typeof(FusionSessionLauncher).GetMethod("MatchesLoadout", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        Assert.That(method.Invoke(null, new object[]
        {
            new[] { new StashItem(bone, 3, 7), new StashItem(bone, 2, 1) },
            new[] { new LootEntry(bone, actual) }
        }), Is.EqualTo(matches));
    }

    [Test]
    public void AdmissionAggregate_OverflowRejected()
    {
        Assert.That(ProfileInventoryRules.TryAggregate(new[]
        {
            new StashItem(new LootId("bone"), int.MaxValue, 0), new StashItem(new LootId("bone"), 1, 2)
        }, out _), Is.False);
    }
}
#endif
