using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownPartyContinuationContextTests
{
    [Test]
    public void Create_AcceptsSoloAndDuoButRejectsInvalidRosters()
    {
        var host = new ProfileId("host");
        var client = new ProfileId("client");
        Assert.That(TownPartyContinuationContext.TryCreate(host, new[] { host }, out _), Is.True);
        Assert.That(TownPartyContinuationContext.TryCreate(host, new[] { host, client }, out _), Is.True);
        Assert.That(TownPartyContinuationContext.TryCreate(host, System.Array.Empty<ProfileId>(), out _), Is.False);
        Assert.That(TownPartyContinuationContext.TryCreate(host, new[] { host, client, new ProfileId("third") }, out _), Is.False);
        Assert.That(TownPartyContinuationContext.TryCreate(host, new[] { host, host }, out _), Is.False);
        Assert.That(TownPartyContinuationContext.TryCreate(host, new[] { client }, out _), Is.False);
    }

    [Test]
    public void Create_CopiesPartyRosterWithoutRaidIdentity()
    {
        var host = new ProfileId("host");
        var client = new ProfileId("client");
        var source = new[] { host, client };
        Assert.That(TownPartyContinuationContext.TryCreate(host, source, out var context), Is.True);
        source[1] = new ProfileId("changed");
        Assert.That(context.Members, Is.EqualTo(new[] { host, client }));
        Assert.That(context.HostProfileId, Is.EqualTo(host));
        Assert.That(typeof(TownPartyContinuationContext).GetProperty("OriginRaidCode"), Is.Null);
        Assert.That(typeof(TownPartyContinuationContext).GetProperty("OriginLaunchRevision"), Is.Null);
    }

    [Test]
    public void Matches_RejectsAChangedAuthoritativePartyRoster()
    {
        var host = new ProfileId("host");
        var client = new ProfileId("client");
        TownPartyContinuationContext.TryCreate(host, new[] { host, client }, out var context);
        Assert.That(context.Matches(new TownPartySnapshot(8, host, new[] { host, client }, 3)), Is.True);
        Assert.That(context.Matches(new TownPartySnapshot(8, host, new[] { host }, 4)), Is.False);
    }
}
