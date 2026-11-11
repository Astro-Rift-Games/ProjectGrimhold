#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;

public sealed class RaidDisplayNamePolicyTests
{
    [TestCase(null, "")]
    [TestCase("", "")]
    [TestCase("   ", "")]
    [TestCase("  Ana ", "Ana")]
    [TestCase("An\na\t\u0000\u202E", "Ana")]
    [TestCase("\u0007\u200B", "")]
    public void Sanitize_StripsControlCharactersAndTrims(string raw, string expected)
    {
        Assert.AreEqual(expected, RaidDisplayNamePolicy.Sanitize(raw));
    }

    [Test]
    public void Sanitize_ClampsToMaxLength()
    {
        string result = RaidDisplayNamePolicy.Sanitize(new string('x', 200));

        Assert.AreEqual(RaidDisplayNamePolicy.MaxLength, result.Length);
    }

    [Test]
    public void Sanitize_ClampNeverSplitsSurrogatePair()
    {
        string raw = new string('x', RaidDisplayNamePolicy.MaxLength - 1) + "\U0001F600\U0001F600";

        string result = RaidDisplayNamePolicy.Sanitize(raw);

        Assert.IsFalse(char.IsHighSurrogate(result[result.Length - 1]));
        Assert.LessOrEqual(result.Length, RaidDisplayNamePolicy.MaxLength);
    }

    [Test]
    public void AdmissionData_WithDisplayName_KeepsIdentityAndSanitizesName()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile-1"),
            "reservation-1",
            Array.Empty<LootEntry>(),
            default,
            level: 3);

        RaidAdmissionData named = source.WithDisplayName("  Ana\n");

        Assert.AreEqual("Ana", named.DisplayName);
        Assert.AreEqual(string.Empty, source.DisplayName);
        Assert.AreEqual(source.ProfileId, named.ProfileId);
        Assert.AreEqual(source.ReservationId, named.ReservationId);
        Assert.AreEqual(source.Level, named.Level);
    }
}
#endif
