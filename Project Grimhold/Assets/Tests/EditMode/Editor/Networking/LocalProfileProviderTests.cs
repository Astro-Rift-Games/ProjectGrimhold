#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

public sealed class LocalProfileProviderTests
{
    [Test]
    public void GetOrCreateLocalProfile_ReturnsStableValidIdentityWithinCurrentRun()
    {
        ProfileId first = LocalProfileProvider.GetOrCreateLocalProfile();
        ProfileId second = LocalProfileProvider.GetOrCreateLocalProfile();

        Assert.That(first.IsValid, Is.True);
        Assert.That(second, Is.EqualTo(first));
        Assert.That(first.Value, Has.Length.EqualTo(32));
    }
}
#endif
