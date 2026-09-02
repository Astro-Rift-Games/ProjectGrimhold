using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

[Category("TASK143")]
public sealed class RaidAdmissionDataCodecTests
{
    [Test]
    public void TryCreate_UsesCompactPreparedEquipmentReferences()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        LootId sword = new("training_sword");
        var reservation = new PendingLoadoutReservation(
            "reservation-prepared",
            new[]
            {
                new StashItem(new LootId("coins"), 4),
                new StashItem(sword, 2)
            },
            new PreparedEquipmentLoadout(sword, sword));

        Assert.That(
            RaidAdmissionData.TryCreate(
                code,
                new ProfileId("profile-prepared"),
                reservation,
                InitialAttributes,
                ExperienceCurve.InitialLevel,
                0,
                0,
                out RaidAdmissionData data),
            Is.True);
        Assert.That(data.WeaponSlot1EntryIndexPlusOne, Is.EqualTo(2));
        Assert.That(data.WeaponSlot2EntryIndexPlusOne, Is.EqualTo(2));
        Assert.That(data.ReservedLoadout[1].Amount, Is.EqualTo(2));
    }

    [Test]
    public void CanonicalCodeToken_RoundTrips()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile-code"),
            "reservation-code",
            new[] { new LootEntry(new LootId("training_sword"), 1) },
            CustomAttributes,
            new[] { 1, 0, 0, 0, 0, 0 },
            level: 2,
            currentExperience: 20,
            lastAppliedProgressionResultSequence: 12);

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(token[0], Is.EqualTo(8));
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.RaidCode, Is.EqualTo(code));
        Assert.That(decoded.ProfileId, Is.EqualTo(source.ProfileId));
        Assert.That(decoded.Level, Is.EqualTo(source.Level));
        Assert.That(decoded.CurrentExperience, Is.EqualTo(source.CurrentExperience));
        Assert.That(
            decoded.LastAppliedProgressionResultSequence,
            Is.EqualTo(source.LastAppliedProgressionResultSequence));
        Assert.That(decoded.CharacterAttributes, Is.EqualTo(CustomAttributes));
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
            new[] { new LootEntry(new LootId("training_sword"), 1) },
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 });

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.RaidCode, Is.Not.EqualTo(second));
    }

    [Test]
    public void Encode_RejectsInvalidProgressionBaselineOrWatermark()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        LootEntry[] loadout = { new(new LootId("training_sword"), 1) };
        int[] equipment = { 1, 0, 0, 0, 0, 0 };
        var invalidBaseline = new RaidAdmissionData(
            code,
            new ProfileId("profile-invalid-baseline"),
            "reservation",
            loadout,
            InitialAttributes,
            equipment,
            level: 0);
        var invalidWatermark = new RaidAdmissionData(
            code,
            new ProfileId("profile-invalid-watermark"),
            "reservation",
            loadout,
            InitialAttributes,
            equipment,
            lastAppliedProgressionResultSequence: int.MaxValue);

        Assert.That(RaidAdmissionDataCodec.TryEncode(invalidBaseline, out _), Is.False);
        Assert.That(RaidAdmissionDataCodec.TryEncode(invalidWatermark, out _), Is.False);
    }

    [Test]
    public void RoundTrip_PreservesAllAdmissionFields()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile-a"),
            "reservation-a",
            new[]
            {
                new LootEntry(new LootId("training_sword"), 2),
                new LootEntry(new LootId("coins"), 4)
            },
            CustomAttributes,
            new[] { 1, 1, 0, 0, 0, 0 });

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.ProfileId, Is.EqualTo(source.ProfileId));
        Assert.That(decoded.ReservationId, Is.EqualTo(source.ReservationId));
        Assert.That(decoded.CharacterAttributes, Is.EqualTo(CustomAttributes));
        Assert.That(decoded.ReservedLoadout, Is.EqualTo(source.ReservedLoadout));
        Assert.That(decoded.WeaponSlot1EntryIndexPlusOne, Is.EqualTo(1));
        Assert.That(decoded.WeaponSlot2EntryIndexPlusOne, Is.EqualTo(1));
    }

    [Test]
    public void Decode_RejectsTamperedOrTrailingToken()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile"),
            "reservation",
            new[] { new LootEntry(new LootId("training_sword"), 1) },
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 });
        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);

        token[0]++;
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out _), Is.False);

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out token), Is.True);
        var trailing = new byte[token.Length + 1];
        System.Buffer.BlockCopy(token, 0, trailing, 0, token.Length);
        Assert.That(RaidAdmissionDataCodec.TryDecode(trailing, out _), Is.False);
    }

    [Test]
    public void Encode_RejectsAdmissionWithoutPreparedWeapon()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile-empty"),
            "reservation-empty",
            new List<LootEntry>(),
            InitialAttributes);

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out _), Is.False);
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
            },
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 });
        var oversized = new RaidAdmissionData(
            code,
            new ProfileId("profile"),
            "reservation",
            new[] { new LootEntry(new LootId("coins"), 10000) },
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 });

        Assert.That(RaidAdmissionDataCodec.TryEncode(duplicate, out _), Is.False);
        Assert.That(RaidAdmissionDataCodec.TryEncode(oversized, out _), Is.False);
    }

    [Test]
    public void Encode_RejectsTwoSlotsReferencingOneOwnedUnit()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var invalid = new RaidAdmissionData(
            code,
            new ProfileId("profile"),
            "reservation",
            new[] { new LootEntry(new LootId("training_sword"), 1) },
            InitialAttributes,
            new[] { 1, 1, 0, 0, 0, 0 });

        Assert.That(RaidAdmissionDataCodec.TryEncode(invalid, out _), Is.False);
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

        var tooMany = new RaidAdmissionData(
            code,
            new ProfileId("profile"),
            "reservation",
            entries,
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 });
        Assert.That(RaidAdmissionDataCodec.TryEncode(tooMany, out _), Is.False);
    }

    [Test]
    public void Decode_RejectsUnsupportedVersionAndTruncatedPayload()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile"),
            "reservation",
            new[] { new LootEntry(new LootId("training_sword"), 1) },
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 });
        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);

        token[0] = 1;
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out _), Is.False);

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out token), Is.True);
        var truncated = new byte[token.Length - 1];
        System.Buffer.BlockCopy(token, 0, truncated, 0, truncated.Length);
        Assert.That(RaidAdmissionDataCodec.TryDecode(truncated, out _), Is.False);
    }

    [Test]
    public void Decode_RejectsStructurallyInvalidTransportedAttributeWithoutCorrectingIt()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile-invalid-attribute"),
            "reservation",
            new[] { new LootEntry(new LootId("training_sword"), 1) },
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 });
        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);

        int offset = 1;
        for (int textIndex = 0; textIndex < 3; textIndex++)
        {
            offset += 1 + token[offset];
        }

        offset += sizeof(int) + sizeof(long) + sizeof(int);
        System.Buffer.BlockCopy(
            System.BitConverter.GetBytes(-1),
            0,
            token,
            offset,
            sizeof(int));

        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out _), Is.False);
    }

    [Test]
    public void TryCreate_PreservesConfirmedCharacterAttributesExactly()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        LootId sword = new("training_sword");
        var reservation = new PendingLoadoutReservation(
            "reservation-attributes",
            new[] { new StashItem(sword, 1) },
            new PreparedEquipmentLoadout(sword, default));

        Assert.That(
            RaidAdmissionData.TryCreate(
                code,
                new ProfileId("profile-attributes"),
                reservation,
                CustomAttributes,
                ExperienceCurve.InitialLevel,
                0,
                0,
                out RaidAdmissionData data),
            Is.True);
        Assert.That(data.CharacterAttributes, Is.EqualTo(CustomAttributes));
    }

    private static CharacterAttributeState InitialAttributes =>
        ProgressionBalanceDefaults.InitialCharacterAttributeState;

    private static CharacterAttributeState CustomAttributes
    {
        get
        {
            Assert.That(
                CharacterAttributeState.TryCreate(1, 2, 3, 4, 5, 6, 7, out CharacterAttributeState state),
                Is.True);
            return state;
        }
    }
}
