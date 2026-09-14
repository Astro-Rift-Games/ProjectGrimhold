using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownPartyContinuationClaimRegistryTests
{
    [Test]
    public void Solo_IsReadyToRestoreWithItsOwnSingleClaim()
    {
        RaidCode.TryParse("100001", out RaidCode code);
        var host = new ProfileId("host");
        TownPartyContinuationContext.TryCreate(code, 7, host, new[] { host }, out var context);
        var registry = new TownPartyContinuationClaimRegistry();

        Assert.That(registry.Submit(host, context), Is.EqualTo(TownPartyContinuationClaimResult.ReadyToRestore));
        Assert.That(registry.MarkRestored(context), Is.True);
    }

    [Test]
    public void Duo_RemainsPendingUntilBothMatchingMembersClaim()
    {
        CreateDuo(out var host, out var client, out var context);
        var registry = new TownPartyContinuationClaimRegistry();

        Assert.That(registry.Submit(host, context), Is.EqualTo(TownPartyContinuationClaimResult.Pending));
        Assert.That(registry.Submit(host, context), Is.EqualTo(TownPartyContinuationClaimResult.Pending));
        Assert.That(registry.Submit(client, context), Is.EqualTo(TownPartyContinuationClaimResult.ReadyToRestore));
        Assert.That(registry.MarkRestored(context), Is.True);
        Assert.That(registry.Submit(host, context), Is.EqualTo(TownPartyContinuationClaimResult.AlreadyRestored));
    }

    [Test]
    public void ExplicitlyWithdrawnClaim_CannotParticipateInLaterCompatibleRestoration()
    {
        CreateDuo(out var host, out var client, out var context);
        var registry = new TownPartyContinuationClaimRegistry();

        Assert.That(registry.Submit(host, context), Is.EqualTo(TownPartyContinuationClaimResult.Pending));
        Assert.That(registry.Withdraw(host, context), Is.True);
        Assert.That(registry.Submit(client, context), Is.EqualTo(TownPartyContinuationClaimResult.Pending));
        Assert.That(registry.Submit(host, context), Is.EqualTo(TownPartyContinuationClaimResult.Rejected));
        Assert.That(registry.MarkRestored(context), Is.False);
    }

    [Test]
    public void SameOriginWithIncompatibleDescriptor_IsRejected()
    {
        CreateDuo(out var host, out _, out var context);
        TownPartyContinuationContext.TryCreate(
            context.OriginRaidCode,
            context.OriginLaunchRevision,
            host,
            new[] { host, new ProfileId("other") },
            out var incompatible);
        var registry = new TownPartyContinuationClaimRegistry();

        Assert.That(registry.Submit(host, context), Is.EqualTo(TownPartyContinuationClaimResult.Pending));
        Assert.That(registry.Submit(host, incompatible), Is.EqualTo(TownPartyContinuationClaimResult.Rejected));
    }

    private static void CreateDuo(
        out ProfileId host,
        out ProfileId client,
        out TownPartyContinuationContext context)
    {
        RaidCode.TryParse("100001", out RaidCode code);
        host = new ProfileId("host");
        client = new ProfileId("client");
        TownPartyContinuationContext.TryCreate(code, 7, host, new[] { host, client }, out context);
    }
}
