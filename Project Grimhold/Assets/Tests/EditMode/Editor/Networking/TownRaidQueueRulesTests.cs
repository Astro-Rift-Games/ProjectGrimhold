using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownRaidQueueRulesTests
{
    [Test]
    public void Join_RejectsDuplicatesAndCapacity()
    {
        Assert.That(TownRaidQueueRules.CanJoin(TownRaidQueueState.Forming, 1, 4, true), Is.False);
        Assert.That(TownRaidQueueRules.CanJoin(TownRaidQueueState.Forming, 4, 4, false), Is.False);
        Assert.That(TownRaidQueueRules.CanJoin(TownRaidQueueState.Forming, 3, 4, false), Is.True);
    }

    [Test]
    public void Launch_RequiresHostAtLeastOneMemberAndReadyCohort()
    {
        Assert.That(TownRaidQueueRules.CanLaunch(TownRaidQueueState.Forming, true, 1, true), Is.True);
        Assert.That(TownRaidQueueRules.CanLaunch(TownRaidQueueState.Forming, false, 1, true), Is.False);
        Assert.That(TownRaidQueueRules.CanLaunch(TownRaidQueueState.Forming, true, 0, true), Is.False);
        Assert.That(TownRaidQueueRules.CanLaunch(TownRaidQueueState.Forming, true, 2, false), Is.False);
    }

    [Test]
    public void Departure_DissolvesOnlyForHost()
    {
        Assert.That(TownRaidQueueRules.ShouldDissolveAfterDeparture(TownRaidQueueState.Forming, true), Is.True);
        Assert.That(TownRaidQueueRules.ShouldDissolveAfterDeparture(TownRaidQueueState.Launching, true), Is.True);
        Assert.That(TownRaidQueueRules.ShouldDissolveAfterDeparture(TownRaidQueueState.Forming, false), Is.False);
        Assert.That(TownRaidQueueRules.ShouldDissolveAfterDeparture(TownRaidQueueState.Launching, false), Is.False);
    }

    [Test]
    public void AuthorityTransfer_CancelsOnlyLaunchingCohort()
    {
        Assert.That(TownRaidQueueRules.ShouldCancelAfterAuthorityTransfer(TownRaidQueueState.Forming), Is.False);
        Assert.That(TownRaidQueueRules.ShouldCancelAfterAuthorityTransfer(TownRaidQueueState.Launching), Is.True);
    }
}
