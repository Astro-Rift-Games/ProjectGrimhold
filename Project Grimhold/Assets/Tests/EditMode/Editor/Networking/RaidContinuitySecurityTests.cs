using System;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

[TestFixture]
public class RaidContinuitySecurityTests
{
    private RaidCode _raidCode;
    private ProfileId _victimProfile;
    private ProfileId _impostorProfile;
    private string _raidGenId;
    private string _legitimateReservationId;
    private string _impostorReservationId;

    [SetUp]
    public void SetUp()
    {
        RaidCode.TryParse("123456", out _raidCode);
        _victimProfile = new ProfileId("victim_profile_reconnect_1");
        _impostorProfile = new ProfileId("impostor_profile_reconnect_2");
        _raidGenId = "raid_gen_test_99999";
        _legitimateReservationId = "res_legitimate_proof_1234";
        _impostorReservationId = "res_forged_proof_9999";
    }

    [Test]
    public void ReconnectPayload_CodecRoundtrip_PreservesReservationProof()
    {
        var payload = new RaidReconnectPayload(_raidCode, _victimProfile, _raidGenId, _legitimateReservationId);
        Assert.IsTrue(payload.IsValid);

        bool encoded = RaidReconnectPayloadCodec.TryEncode(payload, out byte[] token);
        Assert.IsTrue(encoded);
        Assert.IsNotNull(token);

        bool decoded = RaidReconnectPayloadCodec.TryDecode(token, out RaidReconnectPayload decodedPayload);
        Assert.IsTrue(decoded);
        Assert.IsTrue(decodedPayload.IsValid);
        Assert.AreEqual(_raidCode, decodedPayload.RaidCode);
        Assert.AreEqual(_victimProfile, decodedPayload.ProfileId);
        Assert.AreEqual(_raidGenId, decodedPayload.RaidGenerationId);
        Assert.AreEqual(_legitimateReservationId, decodedPayload.ReservationId);
    }

    [Test]
    public void Impostor_WithMismatchedReservationId_DiffersFromLegitimatePayload()
    {
        var legitimate = new RaidReconnectPayload(_raidCode, _victimProfile, _raidGenId, _legitimateReservationId);
        var impostor = new RaidReconnectPayload(_raidCode, _victimProfile, _raidGenId, _impostorReservationId);

        Assert.AreNotEqual(legitimate, impostor, "Payloads with different ReservationId proofs must not be equal.");
        Assert.AreNotEqual(legitimate.ReservationId, impostor.ReservationId);
    }

    [Test]
    public void ReconnectTicket_StoreRoundtrip_PreservesReservationProof()
    {
        RaidReconnectTicketStore.Clear();

        var ticket = new RaidReconnectTicket(
            _raidCode,
            _victimProfile,
            _raidGenId,
            "session-reconnect-1",
            _legitimateReservationId);

        RaidReconnectTicketStore.Save(ticket);

        bool loaded = RaidReconnectTicketStore.TryLoad(_victimProfile, out RaidReconnectTicket loadedTicket);
        Assert.IsTrue(loaded);
        Assert.AreEqual(_victimProfile, loadedTicket.ProfileId);
        Assert.AreEqual(_legitimateReservationId, loadedTicket.ReservationId);

        RaidReconnectPayload payload = loadedTicket.ToPayload();
        Assert.AreEqual(_legitimateReservationId, payload.ReservationId);

        RaidReconnectTicketStore.Clear();
    }

    [Test]
    public void HostMigration_Token_AuthoritativeAdmissionTicketDecodesCleanly()
    {
        CharacterAttributeState.TryCreate(5, 5, 5, 5, 5, 5, 0, out CharacterAttributeState attributes);
        var equipment = new PreparedEquipmentLoadout(weaponSetAMainHand: new LootId("iron_sword"), weaponSetBMainHand: default);

        string ticketString = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(
            _victimProfile,
            _raidCode,
            _legitimateReservationId,
            level: 1,
            experience: 0,
            attributes: attributes,
            abilities: default,
            items: new[] { new LootEntry(new LootId("iron_sword"), 1) },
            equipment: equipment);

        byte[] token = System.Text.Encoding.UTF8.GetBytes(ticketString);

        bool decoded = AuthoritativeRaidAdmissionTicketCodec.TryDecode(token, out AuthoritativeRaidAdmissionTicket authTicket);
        Assert.IsTrue(decoded, "Authoritative ticket token must decode for host migration verification.");
        Assert.AreEqual(_victimProfile, authTicket.ProfileId);
        Assert.AreEqual(_raidCode, authTicket.RaidCode);
        Assert.AreEqual(_legitimateReservationId, authTicket.ReservationId);
    }

    [Test]
    public void ReconnectReservationProof_WhenParticipantHasReservationAndPayloadReservationIsEmpty_IsRejected()
    {
        Assert.IsFalse(
            NetworkSpawnManager.IsReconnectReservationProofValid(_legitimateReservationId, string.Empty),
            "An empty payload ReservationId must not bypass an active participant reservation proof.");
        Assert.IsFalse(
            NetworkSpawnManager.IsReconnectReservationProofValid(_legitimateReservationId, null),
            "A null payload ReservationId must not bypass an active participant reservation proof.");
        Assert.IsFalse(
            NetworkSpawnManager.IsReconnectReservationProofValid(_legitimateReservationId, _impostorReservationId),
            "A mismatched payload ReservationId must be rejected.");
        Assert.IsTrue(
            NetworkSpawnManager.IsReconnectReservationProofValid(_legitimateReservationId, _legitimateReservationId),
            "Matching ReservationId proof must be accepted.");
    }

    [Test]
    public void HostMigration_Token_ExpiredAuthoritativeAdmissionTicket_IsRejected()
    {
        RaidTeamId.TryCreate(1, out RaidTeamId teamId);
        RaidLaunchContext.TryCreate(
            _raidCode,
            _victimProfile,
            new[] { new RaidLaunchParticipant(_victimProfile, teamId) },
            _victimProfile,
            1,
            out RaidLaunchContext launchContext);

        CharacterAttributeState.TryCreate(5, 5, 5, 5, 5, 5, 0, out CharacterAttributeState attributes);
        var equipment = new PreparedEquipmentLoadout(weaponSetAMainHand: new LootId("iron_sword"), weaponSetBMainHand: default);

        string expiredTicketString = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(
            _victimProfile,
            _raidCode,
            _legitimateReservationId,
            level: 1,
            experience: 0,
            attributes: attributes,
            abilities: default,
            items: new[] { new LootEntry(new LootId("iron_sword"), 1) },
            equipment: equipment,
            ttlSeconds: -120);

        bool decoded = AuthoritativeRaidAdmissionTicketCodec.TryDecode(expiredTicketString, out AuthoritativeRaidAdmissionTicket expiredTicket);
        Assert.IsTrue(decoded);

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.IsFalse(
            NetworkSpawnManager.IsAuthoritativeHostMigrationTicketValid(in expiredTicket, launchContext, now),
            "Expired AuthoritativeRaidAdmissionTicket must be rejected during Host Migration token validation.");
    }
}
