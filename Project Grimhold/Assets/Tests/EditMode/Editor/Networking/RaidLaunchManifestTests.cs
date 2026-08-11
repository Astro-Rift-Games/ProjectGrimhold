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

    [Test]
    public void CodeAdmissionManifest_AllowsAnyValidProfileWithoutFrozenCohort()
    {
        RaidLaunchManifest manifest = RaidLaunchManifest.CreateCodeAdmission(
            "raid-123456", "raid-123456", "code-123456", 1);

        Assert.That(manifest.IsValid, Is.True);
        Assert.That(manifest.AllowsCodeAdmission, Is.True);
        Assert.That(manifest.AdmittedProfiles, Is.Empty);
        Assert.That(manifest.Contains(new ProfileId("client")), Is.True);
    }

    [TestCase("123456", "123456")]
    [TestCase(" 001234 ", "001234")]
    public void RaidCode_NormalizesExactlySixDigits(string source, string expected)
    {
        Assert.That(RaidLaunchManifest.Code.TryNormalize(source, out string code), Is.True);
        Assert.That(code, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("12345")]
    [TestCase("1234567")]
    [TestCase("12A456")]
    public void RaidCode_RejectsInvalidValues(string source)
    {
        Assert.That(RaidLaunchManifest.Code.TryNormalize(source, out _), Is.False);
    }

    [Test]
    public void RaidCode_CreatesDeterministicManifestIdentity()
    {
        RaidLaunchManifest host = RaidLaunchManifest.Code.CreateManifest("654321");
        RaidLaunchManifest client = RaidLaunchManifest.Code.CreateManifest(" 654321 ");

        Assert.That(host.IsValid, Is.True);
        Assert.That(client.SessionName, Is.EqualTo(host.SessionName));
        Assert.That(client.RaidId, Is.EqualTo(host.RaidId));
        Assert.That(host.AccessSecret, Is.Null);
        Assert.That(client.AccessSecret, Is.Null);
    }
}
