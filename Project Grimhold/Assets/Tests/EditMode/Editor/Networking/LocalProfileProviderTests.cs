#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

[Category("TASK143")]
public sealed class LocalProfileProviderTests
{
    [SetUp]
    public void SetUp()
    {
        ResetCachedIdentity();
    }

    [TearDown]
    public void TearDown()
    {
        ResetCachedIdentity();
    }

    [Test]
    public void GetOrCreateLocalProfile_BeforeAuthentication_ReturnsInvalidIdentity()
    {
        ProfileId profileId = LocalProfileProvider.GetOrCreateLocalProfile();

        Assert.That(profileId.IsValid, Is.False);
    }

    [Test]
    public void SetRemoteCharacterId_UsesBackendIdentityUntilCleared()
    {
        var characterId = new ProfileId("remote-character-id");

        LocalProfileProvider.SetRemoteCharacterId(characterId);

        Assert.That(LocalProfileProvider.GetOrCreateLocalProfile(), Is.EqualTo(characterId));

        LocalProfileProvider.ClearRemoteCharacterId();

        Assert.That(LocalProfileProvider.GetOrCreateLocalProfile().IsValid, Is.False);
    }

    private static void ResetCachedIdentity()
    {
        LocalProfileProvider.ClearRemoteCharacterId();
    }
}
#endif
