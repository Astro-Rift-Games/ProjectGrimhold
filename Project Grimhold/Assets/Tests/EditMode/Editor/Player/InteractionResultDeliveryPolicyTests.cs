using NUnit.Framework;

public sealed class InteractionResultDeliveryPolicyTests
{
    [TestCase(true, true, true, false)]
    [TestCase(true, false, false, true)]
    [TestCase(false, true, false, false)]
    [TestCase(false, false, false, false)]
    public void RoutesOnlyAuthoritativeResults(
        bool hasStateAuthority,
        bool hasInputAuthority,
        bool expectedLocal,
        bool expectedRemote)
    {
        Assert.That(
            InteractionResultDeliveryPolicy.ShouldEnqueueLocally(hasStateAuthority, hasInputAuthority),
            Is.EqualTo(expectedLocal));
        Assert.That(
            InteractionResultDeliveryPolicy.ShouldSendRemote(hasStateAuthority, hasInputAuthority),
            Is.EqualTo(expectedRemote));
    }
}
