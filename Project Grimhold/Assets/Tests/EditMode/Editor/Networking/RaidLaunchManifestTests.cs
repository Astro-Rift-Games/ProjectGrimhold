using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class RaidLaunchManifestTests
{
    [Test]
    public void ValidManifest_ContainsEveryAdmittedProfile()
    {
        var host = new ProfileId("host");
        var client = new ProfileId("client");
        var manifest = new RaidLaunchManifest(
            "raid", "session", "secret", host,
            new[] { host, client }, 1);

        Assert.That(manifest.IsValid, Is.True);
        Assert.That(manifest.Contains(host), Is.True);
        Assert.That(manifest.Contains(client), Is.True);
    }

    [Test]
    public void Manifest_RejectsDuplicateProfilesAndMissingHost()
    {
        var host = new ProfileId("host");
        var duplicate = new RaidLaunchManifest(
            "raid", "session", "secret", host,
            new[] { host, host }, 1);
        var missingHost = new RaidLaunchManifest(
            "raid", "session", "secret", host,
            new[] { new ProfileId("client") }, 1);

        Assert.That(duplicate.IsValid, Is.False);
        Assert.That(missingHost.IsValid, Is.False);
    }
}
