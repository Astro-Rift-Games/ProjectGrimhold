using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

[Category("TASK143")]
[Category("TASK180")]
public sealed class RaidAdmissionDataCodecTests
{
    private const string ReservationId = "0123456789abcdef0123456789abcdef";

    [Test]
    public void TryCreate_UsesCompactPreparedEquipmentReferences()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        LootId sword = new("arming_sword");
        var reservation = new PendingLoadoutReservation(
            ReservationId,
            new[]
            {
                new StashItem(new LootId("coins"), 4)
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
        Assert.That(data.WeaponSetAMainHandEntryIndexPlusOne, Is.EqualTo(2));
        Assert.That(data.WeaponSetBMainHandEntryIndexPlusOne, Is.EqualTo(2));
        Assert.That(data.ReservedLoadout[1].Amount, Is.EqualTo(2));
    }

    [Test]
    public void CanonicalCodeToken_RoundTrips()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile-code"),
            ReservationId,
            new[] { new LootEntry(new LootId("arming_sword"), 1) },
            CustomAttributes,
            new[] { 1, 0, 0, 0, 0, 0 },
            level: 2,
            currentExperience: 20,
            lastAppliedProgressionResultSequence: 12);

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(token[0], Is.EqualTo(11));
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
        Assert.That(decoded.ActiveWeaponSet, Is.EqualTo(WeaponSetSlot.SetA));
    }

    [Test]
    public void CanonicalCodeToken_RejectsDifferentCodeAtAuthoritativeBoundary()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode first), Is.True);
        Assert.That(RaidCode.TryParse("038272", out RaidCode second), Is.True);
        var source = new RaidAdmissionData(
            first,
            new ProfileId("profile-code"),
            ReservationId,
            new[] { new LootEntry(new LootId("arming_sword"), 1) },
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
        LootEntry[] loadout = { new(new LootId("arming_sword"), 1) };
        int[] equipment = { 1, 0, 0, 0, 0, 0 };
        var invalidBaseline = new RaidAdmissionData(
            code,
            new ProfileId("profile-invalid-baseline"),
            ReservationId,
            loadout,
            InitialAttributes,
            equipment,
            level: 0);
        var invalidWatermark = new RaidAdmissionData(
            code,
            new ProfileId("profile-invalid-watermark"),
            ReservationId,
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
            ReservationId,
            new[]
            {
                new LootEntry(new LootId("arming_sword"), 3),
                new LootEntry(new LootId("coins"), 4)
            },
            CustomAttributes,
            new[] { 1, 1, 0, 0, 0, 0, 1, 0 },
            activeWeaponSet: WeaponSetSlot.SetB);

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.ProfileId, Is.EqualTo(source.ProfileId));
        Assert.That(decoded.ReservationId, Is.EqualTo(source.ReservationId));
        Assert.That(decoded.CharacterAttributes, Is.EqualTo(CustomAttributes));
        Assert.That(decoded.ReservedLoadout, Is.EqualTo(source.ReservedLoadout));
        Assert.That(decoded.WeaponSetAMainHandEntryIndexPlusOne, Is.EqualTo(1));
        Assert.That(decoded.WeaponSetBMainHandEntryIndexPlusOne, Is.EqualTo(1));
        Assert.That(decoded.WeaponSetAOffHandEntryIndexPlusOne, Is.EqualTo(1));
        Assert.That(decoded.ActiveWeaponSet, Is.EqualTo(WeaponSetSlot.SetB));
    }

    [Test]
    public void Decode_RejectsTamperedOrTrailingToken()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile"),
            ReservationId,
            new[] { new LootEntry(new LootId("arming_sword"), 1) },
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
            ReservationId,
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
            ReservationId,
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
            ReservationId,
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
            ReservationId,
            new[] { new LootEntry(new LootId("arming_sword"), 1) },
            InitialAttributes,
            new[] { 1, 1, 0, 0, 0, 0 });

        Assert.That(RaidAdmissionDataCodec.TryEncode(invalid, out _), Is.False);
    }

    [Test]
    public void Encode_RejectsMoreThanMaximumEntries()
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
            ReservationId,
            entries,
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 });
        Assert.That(RaidAdmissionDataCodec.TryEncode(tooMany, out _), Is.False);
    }

    [Test]
    public void ThirtyEntryInventory_RoundTripsWithinExpandedTokenBudget()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var entries = new List<LootEntry>
        {
            new(new LootId("arming_sword"), 1)
        };
        for (int index = 1; index < LocalProfileSnapshot.MaxLoadoutSlots; index++)
        {
            entries.Add(new LootEntry(new LootId($"inventory_item_{index:D2}"), 1));
        }

        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile-capacity"),
            ReservationId,
            entries,
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 });

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(token.Length, Is.GreaterThan(512));
        Assert.That(token.Length, Is.LessThanOrEqualTo(RaidLoadoutRules.MaximumTokenBytes));
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.ReservedLoadout, Has.Count.EqualTo(LocalProfileSnapshot.MaxLoadoutSlots));
    }

    [Test]
    public void Decode_RejectsUnsupportedVersionAndTruncatedPayload()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile"),
            ReservationId,
            new[] { new LootEntry(new LootId("arming_sword"), 1) },
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
            ReservationId,
            new[] { new LootEntry(new LootId("arming_sword"), 1) },
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 });
        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);

        int offset = 1;
        for (int textIndex = 0; textIndex < 2; textIndex++)
        {
            offset += 1 + token[offset];
        }

        offset += 16;
        offset += sizeof(int) + sizeof(long) + sizeof(int);
        System.Buffer.BlockCopy(
            System.BitConverter.GetBytes((short)-1),
            0,
            token,
            offset,
            sizeof(short));

        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out _), Is.False);
    }

    [Test]
    public void TryCreate_PreservesConfirmedCharacterAttributesExactly()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        LootId sword = new("arming_sword");
        var reservation = new PendingLoadoutReservation(
            ReservationId,
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

    [TestCase(null, null)]
    [TestCase("charge", null)]
    [TestCase(null, "trap")]
    [TestCase("charge", "trap")]
    public void PreparedAbilitySlots_RoundTripZeroOneOrTwoOccupiedSlots(
        string slot1,
        string slot2)
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var prepared = new PreparedAbilityLoadout(
            string.IsNullOrEmpty(slot1) ? default : new AbilityId(slot1),
            string.IsNullOrEmpty(slot2) ? default : new AbilityId(slot2));
        var source = new RaidAdmissionData(
            code,
            new ProfileId("profile-abilities"),
            ReservationId,
            new[] { new LootEntry(new LootId("arming_sword"), 1) },
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 },
            preparedAbilities: prepared);

        Assert.That(RaidAdmissionDataCodec.TryEncode(source, out byte[] token), Is.True);
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(decoded.PreparedAbilities, Is.EqualTo(prepared));
    }

    [Test]
    public void PreparedAbilitySlots_RejectDuplicateAndMalformedTransport()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var duplicate = new RaidAdmissionData(
            code,
            new ProfileId("profile-duplicate-abilities"),
            ReservationId,
            new[] { new LootEntry(new LootId("arming_sword"), 1) },
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 },
            preparedAbilities: new PreparedAbilityLoadout(
                new AbilityId("charge"),
                new AbilityId("charge")));
        Assert.That(RaidAdmissionDataCodec.TryEncode(duplicate, out _), Is.False);

        var valid = new RaidAdmissionData(
            code,
            new ProfileId("profile-malformed-abilities"),
            ReservationId,
            new[] { new LootEntry(new LootId("arming_sword"), 1) },
            InitialAttributes,
            new[] { 1, 0, 0, 0, 0, 0 });
        Assert.That(RaidAdmissionDataCodec.TryEncode(valid, out byte[] token), Is.True);

        int offset = 1;
        for (int textIndex = 0; textIndex < 2; textIndex++)
        {
            offset += 1 + token[offset];
        }
        offset += 16;
        offset += sizeof(int) + sizeof(long) + sizeof(int);
        offset += sizeof(short) * 7;
        token[offset] = 2;

        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out _), Is.False);
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
