using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class RaidAdmissionDataCodecTests
{
    [Test]
    public void CanonicalCodeToken_RoundTrips()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile-code"),
            "reservation-code",
            new[] { new LootEntry(new LootId("coins"), 4) });

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(token[0], Is.EqualTo(4));
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.RaidCode, Is.EqualTo(code));
        Assert.That(decoded.ProfileId, Is.EqualTo(source.ProfileId));
        Assert.That(decoded.ReservationId, Is.EqualTo(source.ReservationId));
    }

    [Test]
    public void CanonicalCodeToken_RejectsDifferentCodeAtAuthoritativeBoundary()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode first), Is.True);
        Assert.That(RaidCode.TryParse("038272", out RaidCode second), Is.True);
        var source = new RaidAdmissionData(
            first,
            new ProfileId("profile-code"),
            "reservation-code",
            new List<LootEntry>());

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.RaidCode, Is.Not.EqualTo(second));
    }

    [Test]
    public void RoundTrip_PreservesAllAdmissionFields()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile-a"),
            "reservation-a",
            new[] { new LootEntry(new LootId("coins"), 4) });

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.ProfileId, Is.EqualTo(source.ProfileId));
        Assert.That(decoded.ReservationId, Is.EqualTo(source.ReservationId));
        Assert.That(decoded.ReservedLoadout, Is.EqualTo(source.ReservedLoadout));
    }

    [Test]
    public void Decode_RejectsTamperedOrTrailingToken()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(code, new ProfileId("profile"), "reservation", new List<LootEntry>());
        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);

        token[0]++;
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out _), Is.False);

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out token), Is.True);
        var trailing = new byte[token.Length + 1];
        System.Buffer.BlockCopy(token, 0, trailing, 0, token.Length);
        Assert.That(RaidAdmissionDataCodec.TryDecode(trailing, out _), Is.False);
    }

    [Test]
    public void RoundTrip_AllowsEmptyLoadout()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile-empty"),
            "reservation-empty",
            new List<LootEntry>());

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.ReservedLoadout, Is.Empty);
    }

    [Test]
    public void Encode_RejectsDuplicateOrOversizedQuantities()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var duplicate = new RaidAdmissionData(
            code,
            new ProfileId("profile"),
            "reservation",
            new[]
            {
                new LootEntry(new LootId("coins"), 1),
                new LootEntry(new LootId("coins"), 2)
            });
        var oversized = new RaidAdmissionData(
            code,
            new ProfileId("profile"),
            "reservation",
            new[] { new LootEntry(new LootId("coins"), 10000) });

        Assert.That(RaidAdmissionDataCodec.TryEncode(duplicate, out _), Is.False);
        Assert.That(RaidAdmissionDataCodec.TryEncode(oversized, out _), Is.False);
    }

    [Test]
    public void Encode_RejectsMoreThanSixteenEntries()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var entries = new List<LootEntry>();
        for (int index = 0; index < RaidLoadoutRules.MaximumEntries + 1; index++)
        {
            entries.Add(new LootEntry(new LootId($"loot-{index}"), 1));
        }

        var tooMany = new RaidAdmissionData(code, new ProfileId("profile"), "reservation", entries);
        Assert.That(RaidAdmissionDataCodec.TryEncode(tooMany, out _), Is.False);
    }

    [Test]
    public void Decode_RejectsUnsupportedVersionAndTruncatedPayload()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(code, new ProfileId("profile"), "reservation", new List<LootEntry>());
        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);

        token[0] = 1;
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out _), Is.False);

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out token), Is.True);
        var truncated = new byte[token.Length - 1];
        System.Buffer.BlockCopy(token, 0, truncated, 0, truncated.Length);
        Assert.That(RaidAdmissionDataCodec.TryDecode(truncated, out _), Is.False);
    }
}
