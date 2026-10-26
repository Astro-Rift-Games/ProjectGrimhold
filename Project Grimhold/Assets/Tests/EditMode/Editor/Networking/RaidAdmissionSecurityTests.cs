using System;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

[TestFixture]
public class RaidAdmissionSecurityTests
{
    private RaidCode _raidCode;
    private ProfileId _hostProfileId;
    private ProfileId _clientProfileId;
    private RaidLaunchContext _launchContext;
    private string _reservationId;

    [SetUp]
    public void SetUp()
    {
        RaidCode.TryParse("123456", out _raidCode);
        _hostProfileId = new ProfileId("host_profile_11111");
        _clientProfileId = new ProfileId("client_profile_22222");
        _reservationId = "res_abcd_1234567890";

        RaidTeamId.TryCreate(1, out RaidTeamId teamId);
        RaidLaunchContext.TryCreate(
            _raidCode,
            _hostProfileId,
            new[]
            {
                new RaidLaunchParticipant(_hostProfileId, teamId),
                new RaidLaunchParticipant(_clientProfileId, teamId)
            },
            _hostProfileId,
            1,
            out _launchContext);
    }

    [Test]
    public void Client_AuthoritativeTicket_ValidCohortAndSnapshot_ResolvesCanonicalAdmission()
    {
        CharacterAttributeState.TryCreate(7, 6, 8, 5, 5, 5, 2, out CharacterAttributeState backendAttributes);
        var backendAbilities = new PreparedAbilityLoadout(new AbilityId("charge"), default);
        var backendItems = new[] { new LootEntry(new LootId("iron_sword"), 1), new LootEntry(new LootId("health_potion"), 2) };
        var backendEquipment = new PreparedEquipmentLoadout(weaponSetAMainHand: new LootId("iron_sword"), weaponSetBMainHand: default);

        string ticketString = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(
            _clientProfileId,
            _raidCode,
            _reservationId,
            level: 1,
            experience: 50,
            attributes: backendAttributes,
            abilities: backendAbilities,
            items: backendItems,
            equipment: backendEquipment);

        bool decoded = AuthoritativeRaidAdmissionTicketCodec.TryDecode(ticketString, out AuthoritativeRaidAdmissionTicket ticket);
        Assert.IsTrue(decoded, "Authoritative ticket must decode successfully.");
        Assert.IsTrue(ticket.IsValid, "Ticket must report valid.");
        Assert.AreEqual(_clientProfileId, ticket.ProfileId);
        Assert.AreEqual(_raidCode, ticket.RaidCode);
        Assert.AreEqual(_reservationId, ticket.ReservationId);
        Assert.AreEqual(1, ticket.Level);
        Assert.AreEqual(50, ticket.Experience);
        Assert.AreEqual(8, ticket.CharacterAttributes.Strength);

        bool admittedByCohort = RaidAdmissionRules.IsAdmitted(_launchContext, ticket);
        Assert.IsTrue(admittedByCohort, "Ticket for client in manifest must be admitted by cohort.");

        bool converted = ticket.TryToRaidAdmissionData(out RaidAdmissionData admissionData);
        Assert.IsTrue(converted, "Canonical RaidAdmissionData must be hydrated from ticket snapshot.");
        Assert.AreEqual(8, admissionData.CharacterAttributes.Strength);
        Assert.AreEqual(1, admissionData.Level);
        Assert.AreEqual(50, admissionData.CurrentExperience);
        Assert.AreEqual(new AbilityId("charge"), admissionData.PreparedAbilities.Slot1);
    }

    [Test]
    public void Client_MismatchedRaidCode_IsRejectedByCohortRules()
    {
        RaidCode.TryParse("654321", out RaidCode foreignRaidCode);
        CharacterAttributeState.TryCreate(5, 5, 5, 5, 5, 5, 0, out CharacterAttributeState attributes);
        var equipment = new PreparedEquipmentLoadout(weaponSetAMainHand: new LootId("iron_sword"), weaponSetBMainHand: default);

        string ticketString = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(
            _clientProfileId,
            foreignRaidCode,
            _reservationId,
            level: 1,
            experience: 0,
            attributes: attributes,
            abilities: default,
            items: new[] { new LootEntry(new LootId("iron_sword"), 1) },
            equipment: equipment);

        AuthoritativeRaidAdmissionTicketCodec.TryDecode(ticketString, out AuthoritativeRaidAdmissionTicket ticket);

        bool admitted = RaidAdmissionRules.IsAdmitted(_launchContext, ticket);
        Assert.IsFalse(admitted, "Ticket issued for a different RaidCode must be rejected.");
    }

    [Test]
    public void Outsider_NotInManifest_IsRejectedByCohortRules()
    {
        ProfileId outsider = new ProfileId("outsider_profile_99999");
        CharacterAttributeState.TryCreate(5, 5, 5, 5, 5, 5, 0, out CharacterAttributeState attributes);
        var equipment = new PreparedEquipmentLoadout(weaponSetAMainHand: new LootId("iron_sword"), weaponSetBMainHand: default);

        string ticketString = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(
            outsider,
            _raidCode,
            _reservationId,
            level: 1,
            experience: 0,
            attributes: attributes,
            abilities: default,
            items: new[] { new LootEntry(new LootId("iron_sword"), 1) },
            equipment: equipment);

        AuthoritativeRaidAdmissionTicketCodec.TryDecode(ticketString, out AuthoritativeRaidAdmissionTicket ticket);

        bool admitted = RaidAdmissionRules.IsAdmitted(_launchContext, ticket);
        Assert.IsFalse(admitted, "Outsider profile not in raid launch manifest must be rejected.");
    }

    [Test]
    public void Verifier_RejectsForgedOrUnregisteredTicket()
    {
        var verifier = new LocalDeterministicRaidAdmissionVerifier();

        CharacterAttributeState.TryCreate(5, 5, 5, 5, 5, 5, 0, out CharacterAttributeState attributes);
        var equipment = new PreparedEquipmentLoadout(weaponSetAMainHand: new LootId("iron_sword"), weaponSetBMainHand: default);

        string forgedTicketString = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(
            _clientProfileId,
            _raidCode,
            _reservationId,
            level: 99,
            experience: 99999,
            attributes: attributes,
            abilities: default,
            items: new[] { new LootEntry(new LootId("iron_sword"), 1) },
            equipment: equipment);

        // Verifier does not have this ticket registered (or registers it as invalid)
        var task = verifier.VerifyRaidAdmissionTicketAsync(forgedTicketString, _raidCode);
        Assert.IsTrue(task.IsCompleted);

        var (valid, _, error) = task.Result;
        Assert.IsFalse(valid, "Unregistered / forged ticket must be rejected by verifier.");
        Assert.AreEqual("UNREGISTERED_TICKET", error);
    }

    [Test]
    public void Verifier_ValidatesRegisteredTicketAndConfirmsAuthoritativeState()
    {
        var verifier = new LocalDeterministicRaidAdmissionVerifier();

        CharacterAttributeState.TryCreate(5, 5, 10, 5, 5, 5, 0, out CharacterAttributeState attributes);
        var equipment = new PreparedEquipmentLoadout(weaponSetAMainHand: new LootId("iron_sword"), weaponSetBMainHand: default);

        string ticketString = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(
            _clientProfileId,
            _raidCode,
            _reservationId,
            level: 2,
            experience: 200,
            attributes: attributes,
            abilities: default,
            items: new[] { new LootEntry(new LootId("iron_sword"), 1) },
            equipment: equipment);

        AuthoritativeRaidAdmissionTicketCodec.TryDecode(ticketString, out AuthoritativeRaidAdmissionTicket ticket);
        verifier.RegisterTicket(ticketString, ticket, valid: true);

        var task = verifier.VerifyRaidAdmissionTicketAsync(ticketString, _raidCode);
        Assert.IsTrue(task.IsCompleted);

        var (valid, verifiedTicket, error) = task.Result;
        Assert.IsTrue(valid, "Legitimate registered ticket must be accepted.");
        Assert.IsNull(error);
        Assert.AreEqual(10, verifiedTicket.CharacterAttributes.Strength);
        Assert.AreEqual(2, verifiedTicket.Level);
    }

    [Test]
    public void Verifier_RejectsTicketWithMismatchedRaidCode()
    {
        var verifier = new LocalDeterministicRaidAdmissionVerifier();
        RaidCode.TryParse("654321", out RaidCode differentRaidCode);

        CharacterAttributeState.TryCreate(5, 5, 5, 5, 5, 5, 0, out CharacterAttributeState attributes);
        var equipment = new PreparedEquipmentLoadout(weaponSetAMainHand: new LootId("iron_sword"), weaponSetBMainHand: default);

        string ticketString = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(
            _clientProfileId,
            differentRaidCode,
            _reservationId,
            level: 1,
            experience: 0,
            attributes: attributes,
            abilities: default,
            items: new[] { new LootEntry(new LootId("iron_sword"), 1) },
            equipment: equipment);

        AuthoritativeRaidAdmissionTicketCodec.TryDecode(ticketString, out AuthoritativeRaidAdmissionTicket ticket);
        verifier.RegisterTicket(ticketString, ticket, valid: true);

        // Verification query asks for _raidCode ("123456"), but ticket is for differentRaidCode ("654321")
        var task = verifier.VerifyRaidAdmissionTicketAsync(ticketString, _raidCode);
        Assert.IsTrue(task.IsCompleted);

        var (valid, _, error) = task.Result;
        Assert.IsFalse(valid, "Ticket for wrong raid code must be rejected by verifier.");
        Assert.AreEqual("RAID_CODE_MISMATCH", error);
    }

    [Test]
    public void TryEvaluateRemoteAdmissionTicket_WithPendingVerifierTask_DefersAndFailsClosedSynchronously()
    {
        CharacterAttributeState.TryCreate(5, 5, 5, 5, 5, 5, 0, out CharacterAttributeState attributes);
        var equipment = new PreparedEquipmentLoadout(weaponSetAMainHand: new LootId("iron_sword"), weaponSetBMainHand: default);

        string ticketString = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(
            _clientProfileId,
            _raidCode,
            _reservationId,
            level: 1,
            experience: 0,
            attributes: attributes,
            abilities: default,
            items: new[] { new LootEntry(new LootId("iron_sword"), 1) },
            equipment: equipment);

        byte[] payload = Encoding.UTF8.GetBytes(ticketString);
        var pendingVerifier = new PendingRaidAdmissionVerifier();

        bool resolved = NetworkSpawnManager.TryEvaluateRemoteAdmissionTicket(
            payload,
            _launchContext,
            pendingVerifier,
            "PlayerRef 2",
            out RaidAdmissionData admissionData,
            out var pendingTask);

        Assert.IsFalse(resolved, "Synchronous evaluation must fail-closed while async verification task is incomplete.");
        Assert.IsFalse(admissionData.IsValid);
        Assert.IsNotNull(pendingTask, "Pending verification task must be returned for async completion handling.");
        Assert.IsFalse(pendingTask.IsCompleted);
    }

    [Test]
    public void TryEvaluateRemoteAdmissionTicket_WhenVerifierActive_RejectsUnauthenticatedClientDeclaredPayload()
    {
        CharacterAttributeState.TryCreate(5, 5, 5, 5, 5, 5, 0, out CharacterAttributeState attributes);
        var equipment = new PreparedEquipmentLoadout(weaponSetAMainHand: new LootId("iron_sword"), weaponSetBMainHand: default);
        string validGuidReservationId = Guid.NewGuid().ToString("N");

        string ticketString = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(
            _clientProfileId,
            _raidCode,
            validGuidReservationId,
            level: 1,
            experience: 0,
            attributes: attributes,
            abilities: default,
            items: new[] { new LootEntry(new LootId("iron_sword"), 1) },
            equipment: equipment);

        Assert.IsTrue(AuthoritativeRaidAdmissionTicketCodec.TryDecode(ticketString, out AuthoritativeRaidAdmissionTicket ticket));
        Assert.IsTrue(ticket.TryToRaidAdmissionData(out RaidAdmissionData clientDeclared));
        Assert.IsTrue(RaidAdmissionDataCodec.TryEncode(clientDeclared, out byte[] payload));

        var verifier = new LocalDeterministicRaidAdmissionVerifier();
        bool resolved = NetworkSpawnManager.TryEvaluateRemoteAdmissionTicket(
            payload,
            _launchContext,
            verifier,
            "PlayerRef 2",
            out RaidAdmissionData admissionData,
            out var pendingTask);

        Assert.IsFalse(resolved, "Unauthenticated client-declared RaidAdmissionData must be rejected when a verifier is active.");
        Assert.IsFalse(admissionData.IsValid);
        Assert.IsNull(pendingTask);
    }

    [Test]
    public void TryEvaluateRemoteAdmissionTicket_WithExpiredAuthoritativeTicket_IsRejected()
    {
        CharacterAttributeState.TryCreate(5, 5, 5, 5, 5, 5, 0, out CharacterAttributeState attributes);
        var equipment = new PreparedEquipmentLoadout(weaponSetAMainHand: new LootId("iron_sword"), weaponSetBMainHand: default);

        string expiredTicketString = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(
            _clientProfileId,
            _raidCode,
            _reservationId,
            level: 1,
            experience: 0,
            attributes: attributes,
            abilities: default,
            items: new[] { new LootEntry(new LootId("iron_sword"), 1) },
            equipment: equipment,
            ttlSeconds: -60);

        byte[] payload = Encoding.UTF8.GetBytes(expiredTicketString);

        bool resolved = NetworkSpawnManager.TryEvaluateRemoteAdmissionTicket(
            payload,
            _launchContext,
            verifier: null,
            "PlayerRef 2",
            out RaidAdmissionData admissionData,
            out _);

        Assert.IsFalse(resolved, "Expired AuthoritativeRaidAdmissionTicket must be rejected.");
        Assert.IsFalse(admissionData.IsValid);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InflatedClientClaims_NullVerifier_IsRejected(bool useTicketFormat)
    {
        Assert.IsTrue(CharacterAttributeState.TryCreate(5, 5, 100, 5, 5, 5, 0, out CharacterAttributeState attributes));
        string raw = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(
            _clientProfileId, _raidCode, Guid.NewGuid().ToString("N"),
            level: 10, experience: 0, attributes: attributes, abilities: default,
            items: new[] { new LootEntry(new LootId("iron_sword"), 1) },
            equipment: new PreparedEquipmentLoadout(weaponSetAMainHand: new LootId("iron_sword"), weaponSetBMainHand: default));
        Assert.IsTrue(AuthoritativeRaidAdmissionTicketCodec.TryDecode(raw, out AuthoritativeRaidAdmissionTicket ticket));
        Assert.IsTrue(ticket.TryToRaidAdmissionData(out RaidAdmissionData clientDeclared));
        Assert.IsTrue(RaidAdmissionRules.IsAdmitted(_launchContext, clientDeclared));
        Assert.IsTrue(RaidAdmissionDataCodec.TryEncode(clientDeclared, out byte[] payload));
        if (useTicketFormat) payload = Encoding.UTF8.GetBytes(raw);

        Assert.IsFalse(NetworkSpawnManager.TryEvaluateRemoteAdmissionTicket(
            payload, _launchContext, null, "PlayerRef 2", out RaidAdmissionData resolved, out var pending));
        Assert.IsFalse(resolved.IsValid);
        Assert.IsNull(pending);
    }

    private sealed class PendingRaidAdmissionVerifier : IRaidAdmissionVerifier
    {
        private readonly System.Threading.Tasks.TaskCompletionSource<(bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)> _tcs =
            new System.Threading.Tasks.TaskCompletionSource<(bool, AuthoritativeRaidAdmissionTicket, string)>();

        public System.Threading.Tasks.Task<(bool valid, AuthoritativeRaidAdmissionTicket ticket, string error)> VerifyRaidAdmissionTicketAsync(
            string rawTicket,
            RaidCode expectedRaidCode) => _tcs.Task;
    }
}
