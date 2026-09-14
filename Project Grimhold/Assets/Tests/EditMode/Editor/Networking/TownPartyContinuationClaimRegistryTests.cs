using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownPartyContinuationClaimRegistryTests
{
    [Test]
    public void Solo_IsReadyToRestoreWithItsOwnSingleClaim()
    {
        var host = new ProfileId("host");
        TownPartyContinuationContext.TryCreate(host, new[] { host }, out var context);
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
        Assert.That(registry.Submit(client, context), Is.EqualTo(TownPartyContinuationClaimResult.ReadyToRestore));
        Assert.That(registry.MarkRestored(context), Is.True);
        Assert.That(registry.Submit(host, context), Is.EqualTo(TownPartyContinuationClaimResult.AlreadyRestored));
    }

    [Test]
    public void WithdrawnClaim_CannotParticipateInLaterRestoration()
    {
        CreateDuo(out var host, out var client, out var context);
        var registry = new TownPartyContinuationClaimRegistry();
        Assert.That(registry.Submit(host, context), Is.EqualTo(TownPartyContinuationClaimResult.Pending));
        Assert.That(registry.Withdraw(host, context), Is.True);
        Assert.That(registry.Submit(client, context), Is.EqualTo(TownPartyContinuationClaimResult.Pending));
        Assert.That(registry.Submit(host, context), Is.EqualTo(TownPartyContinuationClaimResult.Rejected));
        Assert.That(registry.MarkRestored(context), Is.False);
    }

    private static void CreateDuo(out ProfileId host, out ProfileId client, out TownPartyContinuationContext context)
    {
        host = new ProfileId("host");
        client = new ProfileId("client");
        TownPartyContinuationContext.TryCreate(host, new[] { host, client }, out context);
    }
}
