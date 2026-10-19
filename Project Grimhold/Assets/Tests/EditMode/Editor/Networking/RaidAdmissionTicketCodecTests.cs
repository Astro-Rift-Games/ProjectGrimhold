using System;
using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

[Category("US53")]
[Category("TASK180")]
public sealed class RaidAdmissionTicketCodecTests
{
    private const string ReservationGuid = "0123456789abcdef0123456789abcdef";

    [Test]
    public void CanonicalTicket_RoundTrips_AndStaysStrictlyUnder128Bytes()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var profileId = new ProfileId("670de5e42a03362095cfad7e");
        var ticket = new RaidAdmissionTicket(code, profileId, ReservationGuid);

        Assert.That(ticket.IsValid, Is.True);
        Assert.That(RaidAdmissionTicketCodec.TryEncode(ticket, out byte[] token), Is.True);
        Assert.That(token, Is.Not.Null);
        Assert.That(token.Length, Is.LessThanOrEqualTo(128), "Token must strictly stay within Fusion's 128-byte limit.");
        Assert.That(token.Length, Is.LessThanOrEqualTo(64), "Expected lightweight ticket to be ~53 bytes.");
        Assert.That(token[0], Is.EqualTo(RaidAdmissionTicketCodec.CanonicalVersion));

        Assert.That(RaidAdmissionTicketCodec.TryDecode(token, out RaidAdmissionTicket decoded), Is.True);
        Assert.That(decoded.RaidCode, Is.EqualTo(code));
        Assert.That(decoded.ProfileId, Is.EqualTo(profileId));
        Assert.That(decoded.ReservationId, Is.EqualTo(ReservationGuid));
        Assert.That(decoded, Is.EqualTo(ticket));
    }

    [Test]
    public void NonGuidReservationId_RoundTripsSafely()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var profileId = new ProfileId("player-profile-123");
        var ticket = new RaidAdmissionTicket(code, profileId, "custom-reservation-id");

        Assert.That(RaidAdmissionTicketCodec.TryEncode(ticket, out byte[] token), Is.True);
        Assert.That(token.Length, Is.LessThanOrEqualTo(128));

        Assert.That(RaidAdmissionTicketCodec.TryDecode(token, out RaidAdmissionTicket decoded), Is.True);
        Assert.That(decoded.ReservationId, Is.EqualTo("custom-reservation-id"));
        Assert.That(decoded, Is.EqualTo(ticket));
    }

    [Test]
    public void Rejects_InvalidTickets()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var validProfile = new ProfileId("profile-ok");

        var emptyCode = new RaidAdmissionTicket(default, validProfile, ReservationGuid);
        Assert.That(RaidAdmissionTicketCodec.TryEncode(emptyCode, out _), Is.False);

        var emptyProfile = new RaidAdmissionTicket(code, default, ReservationGuid);
        Assert.That(RaidAdmissionTicketCodec.TryEncode(emptyProfile, out _), Is.False);

        var emptyReservation = new RaidAdmissionTicket(code, validProfile, "");
        Assert.That(RaidAdmissionTicketCodec.TryEncode(emptyReservation, out _), Is.False);

        var nullReservation = new RaidAdmissionTicket(code, validProfile, null);
        Assert.That(RaidAdmissionTicketCodec.TryEncode(nullReservation, out _), Is.False);
    }

    [Test]
    public void Rejects_CorruptedToken_AndWrongVersion()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var ticket = new RaidAdmissionTicket(code, new ProfileId("profile-ok"), ReservationGuid);
        Assert.That(RaidAdmissionTicketCodec.TryEncode(ticket, out byte[] token), Is.True);

        // Wrong version
        byte[] wrongVersion = (byte[])token.Clone();
        wrongVersion[0] = 99;
        Assert.That(RaidAdmissionTicketCodec.TryDecode(wrongVersion, out _), Is.False);

        // Null and empty
        Assert.That(RaidAdmissionTicketCodec.TryDecode(null, out _), Is.False);
        Assert.That(RaidAdmissionTicketCodec.TryDecode(Array.Empty<byte>(), out _), Is.False);

        // Truncated token
        byte[] truncated = new byte[token.Length - 5];
        Array.Copy(token, truncated, truncated.Length);
        Assert.That(RaidAdmissionTicketCodec.TryDecode(truncated, out _), Is.False);

        // Trailing garbage
        byte[] trailing = new byte[token.Length + 4];
        Array.Copy(token, trailing, token.Length);
        Assert.That(RaidAdmissionTicketCodec.TryDecode(trailing, out _), Is.False);
    }

    [Test]
    public void FromAdmissionData_CreatesMatchingTicket()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        var profileId = new ProfileId("profile-match");
        var admission = new RaidAdmissionData(
            code,
            profileId,
            ReservationGuid,
            new[] { new LootEntry(new LootId("sword"), 1) },
            default,
            new[] { 1, 0, 0, 0, 0, 0, 0, 0 });

        RaidAdmissionTicket ticket = RaidAdmissionTicket.FromAdmissionData(in admission);
        Assert.That(ticket.IsValid, Is.True);
        Assert.That(ticket.RaidCode, Is.EqualTo(code));
        Assert.That(ticket.ProfileId, Is.EqualTo(profileId));
        Assert.That(ticket.ReservationId, Is.EqualTo(ReservationGuid));
    }

    [Test]
    public void RaidAdmissionRules_IsAdmitted_ValidatesTicketAgainstManifest()
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        Assert.That(RaidCode.TryParse("099999", out RaidCode wrongCode), Is.True);
        var profile1 = new ProfileId("profile-1");
        var profile2 = new ProfileId("profile-2");
        var uninvited = new ProfileId("profile-uninvited");

        RaidTeamId.TryCreate(1, out RaidTeamId teamId);
        Assert.That(
            RaidLaunchContext.TryCreate(
                code,
                profile1,
                new[]
                {
                    new RaidLaunchParticipant(profile1, teamId),
                    new RaidLaunchParticipant(profile2, teamId)
                },
                profile1,
                1,
                out RaidLaunchContext manifest),
            Is.True);

        var validTicket1 = new RaidAdmissionTicket(code, profile1, ReservationGuid);
        var validTicket2 = new RaidAdmissionTicket(code, profile2, ReservationGuid);
        var wrongCodeTicket = new RaidAdmissionTicket(wrongCode, profile1, ReservationGuid);
        var uninvitedTicket = new RaidAdmissionTicket(code, uninvited, ReservationGuid);

        Assert.That(RaidAdmissionRules.IsAdmitted(manifest, validTicket1), Is.True);
        Assert.That(RaidAdmissionRules.IsAdmitted(manifest, validTicket2), Is.True);
        Assert.That(RaidAdmissionRules.IsAdmitted(manifest, wrongCodeTicket), Is.False);
        Assert.That(RaidAdmissionRules.IsAdmitted(manifest, uninvitedTicket), Is.False);
        Assert.That(RaidAdmissionRules.IsAdmitted(null, validTicket1), Is.False);
    }
}
