using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class RaidAdmissionDataCodecTests
{
    [Test]
    public void RoundTrip_PreservesAllAdmissionFields()
    {
        var source = new RaidAdmissionData(
            "raid-a",
            "secret-a",
            new ProfileId("profile-a"),
            PlayerClassId.Ranged);

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.RaidId, Is.EqualTo(source.RaidId));
        Assert.That(decoded.AccessSecret, Is.EqualTo(source.AccessSecret));
        Assert.That(decoded.ProfileId, Is.EqualTo(source.ProfileId));
        Assert.That(decoded.SelectedBuild, Is.EqualTo(source.SelectedBuild));
    }

    [Test]
    public void Decode_RejectsTamperedOrTrailingToken()
    {
        var source = new RaidAdmissionData("raid", "secret", new ProfileId("profile"), PlayerClassId.Melee);
        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);

        token[0]++;
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out _), Is.False);

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out token), Is.True);
        var trailing = new byte[token.Length + 1];
        System.Buffer.BlockCopy(token, 0, trailing, 0, token.Length);
        Assert.That(RaidAdmissionDataCodec.TryDecode(trailing, out _), Is.False);
    }
}
