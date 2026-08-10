using System.Collections.Generic;
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
            PlayerClassId.Ranged,
            "reservation-a",
            new[] { new LootEntry(new LootId("coins"), 4) });

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.RaidId, Is.EqualTo(source.RaidId));
        Assert.That(decoded.AccessSecret, Is.EqualTo(source.AccessSecret));
        Assert.That(decoded.ProfileId, Is.EqualTo(source.ProfileId));
        Assert.That(decoded.SelectedBuild, Is.EqualTo(source.SelectedBuild));
        Assert.That(decoded.ReservationId, Is.EqualTo(source.ReservationId));
        Assert.That(decoded.ReservedLoadout, Is.EqualTo(source.ReservedLoadout));
    }

    [Test]
    public void Decode_RejectsTamperedOrTrailingToken()
    {
        var source = new RaidAdmissionData("raid", "secret", new ProfileId("profile"), PlayerClassId.Melee, "reservation", new List<LootEntry>());
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
        var source = new RaidAdmissionData(
            "raid-empty",
            "secret-empty",
            new ProfileId("profile-empty"),
            PlayerClassId.Melee,
            "reservation-empty",
            new List<LootEntry>());

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.ReservedLoadout, Is.Empty);
    }

    [Test]
    public void Encode_RejectsDuplicateOrOversizedQuantities()
    {
        var duplicate = new RaidAdmissionData(
            "raid",
            "secret",
            new ProfileId("profile"),
            PlayerClassId.Melee,
            "reservation",
            new[]
            {
                new LootEntry(new LootId("coins"), 1),
                new LootEntry(new LootId("coins"), 2)
            });
        var oversized = new RaidAdmissionData(
            "raid",
            "secret",
            new ProfileId("profile"),
            PlayerClassId.Melee,
            "reservation",
            new[] { new LootEntry(new LootId("coins"), 10000) });

        Assert.That(RaidAdmissionDataCodec.TryEncode(duplicate, out _), Is.False);
        Assert.That(RaidAdmissionDataCodec.TryEncode(oversized, out _), Is.False);
    }

    [Test]
    public void Encode_RejectsMoreThanSixteenEntriesAndTokensOverProjectLimit()
    {
        var entries = new List<LootEntry>();
        for (int index = 0; index < RaidLoadoutRules.MaximumEntries + 1; index++)
        {
            entries.Add(new LootEntry(new LootId($"loot-{index}"), 1));
        }

        var tooMany = new RaidAdmissionData("raid", "secret", new ProfileId("profile"), PlayerClassId.Melee, "reservation", entries);
        Assert.That(RaidAdmissionDataCodec.TryEncode(tooMany, out _), Is.False);

        var longSecret = new string('x', RaidLoadoutRules.MaximumTokenBytes);
        var tooLarge = new RaidAdmissionData("raid", longSecret, new ProfileId("profile"), PlayerClassId.Melee, "reservation", new List<LootEntry>());
        Assert.That(RaidAdmissionDataCodec.TryEncode(tooLarge, out _), Is.False);
    }

    [Test]
    public void Decode_RejectsUnsupportedVersionAndTruncatedPayload()
    {
        var source = new RaidAdmissionData("raid", "secret", new ProfileId("profile"), PlayerClassId.Melee, "reservation", new List<LootEntry>());
        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);

        token[0] = 1;
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out _), Is.False);

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out token), Is.True);
        var truncated = new byte[token.Length - 1];
        System.Buffer.BlockCopy(token, 0, truncated, 0, truncated.Length);
        Assert.That(RaidAdmissionDataCodec.TryDecode(truncated, out _), Is.False);
    }
}
