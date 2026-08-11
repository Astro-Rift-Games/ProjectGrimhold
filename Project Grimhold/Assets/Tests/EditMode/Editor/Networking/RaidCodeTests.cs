using NUnit.Framework;

public sealed class RaidCodeTests
{
    [TestCase("000000")]
    [TestCase("000001")]
    [TestCase("038271")]
    [TestCase("999999")]
    public void TryParse_AcceptsExactSixAsciiDigits(string value)
    {
        Assert.That(RaidCode.TryParse(value, out RaidCode code), Is.True);
        Assert.That(code.ToString(), Is.EqualTo(value));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("12345")]
    [TestCase("1234567")]
    [TestCase("12 345")]
    [TestCase("abcdef")]
    [TestCase("12345a")]
    [TestCase("-12345")]
    [TestCase("１２３４５６")]
    public void TryParse_RejectsInvalidValues(string value)
    {
        Assert.That(RaidCode.TryParse(value, out RaidCode code), Is.False);
        Assert.That(code.IsValid, Is.False);
    }

    [Test]
    public void TryParse_TrimsExternalWhitespace()
    {
        Assert.That(RaidCode.TryParse(" 038271 ", out RaidCode code), Is.True);
        Assert.That(code.ToString(), Is.EqualTo("038271"));
    }

    [Test]
    public void SameCode_DerivesStableIdentitiesAndEquality()
    {
        RaidCode.TryParse("038271", out RaidCode first);
        RaidCode.TryParse(" 038271 ", out RaidCode second);

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first.SessionName, Is.EqualTo(second.SessionName));
        Assert.That(first.RaidId, Is.EqualTo(second.RaidId));
    }

    [Test]
    public void DifferentCodes_DeriveDifferentIdentities()
    {
        RaidCode.TryParse("038271", out RaidCode first);
        RaidCode.TryParse("038272", out RaidCode second);

        Assert.That(first.SessionName, Is.Not.EqualTo(second.SessionName));
        Assert.That(first.RaidId, Is.Not.EqualTo(second.RaidId));
    }

    [Test]
    public void Request_UsesRaidCodeAndValidatesRole()
    {
        RaidCode.TryParse("038271", out RaidCode code);
        var host = new RaidConnectionRequest(code, RaidConnectionRole.Host);
        var client = new RaidConnectionRequest(code, RaidConnectionRole.Client);
        var invalidCode = new RaidConnectionRequest(default, RaidConnectionRole.Host);
        var invalidRole = new RaidConnectionRequest(code, RaidConnectionRole.None);

        Assert.That(host.IsValid, Is.True);
        Assert.That(client.IsValid, Is.True);
        Assert.That(invalidCode.IsValid, Is.False);
        Assert.That(invalidRole.IsValid, Is.False);
        Assert.That(host.SessionName, Is.EqualTo(code.SessionName));
        Assert.That(host.RaidId, Is.EqualTo(code.RaidId));
    }
}
