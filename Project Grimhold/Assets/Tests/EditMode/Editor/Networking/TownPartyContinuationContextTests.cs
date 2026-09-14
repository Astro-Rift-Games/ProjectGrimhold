using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownPartyContinuationContextTests
{
    [Test]
    public void Create_AcceptsSoloAndDuoButRejectsInvalidRosters()
    {
        RaidCode.TryParse("100001", out RaidCode code);
        var host = new ProfileId("host");
        var client = new ProfileId("client");

        Assert.That(TownPartyContinuationContext.TryCreate(
            code, 1, host, new[] { host }, out _), Is.True);
        Assert.That(TownPartyContinuationContext.TryCreate(
            code, 1, host, new[] { host, client }, out _), Is.True);
        Assert.That(TownPartyContinuationContext.TryCreate(
            code, 1, host, System.Array.Empty<ProfileId>(), out _), Is.False);
        Assert.That(TownPartyContinuationContext.TryCreate(
            code,
            1,
            host,
            new[] { host, new ProfileId("client-a"), new ProfileId("client-b") },
            out _), Is.False);
        Assert.That(TownPartyContinuationContext.TryCreate(
            code, 1, host, new[] { host, host }, out _), Is.False);
        Assert.That(TownPartyContinuationContext.TryCreate(
            code, 1, host, new[] { client }, out _), Is.False);
        Assert.That(RaidSessionRules.MaxParticipants, Is.EqualTo(16));
    }

    [Test]
    public void Create_CopiesRosterAndPreservesAuthoritativeOrder()
    {
        RaidCode.TryParse("100001", out RaidCode code);
        var host = new ProfileId("host");
        var client = new ProfileId("client");
        var source = new[] { host, client };

        Assert.That(TownPartyContinuationContext.TryCreate(code, 7, host, source, out var context), Is.True);
        source[1] = new ProfileId("changed");

        Assert.That(context.Members, Is.EqualTo(new[] { host, client }));
        Assert.That(context.HostProfileId, Is.EqualTo(host));
        Assert.That(context.OriginLaunchRevision, Is.EqualTo(7));
    }
}
