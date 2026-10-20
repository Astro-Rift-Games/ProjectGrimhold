using System;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

[TestFixture]
public class TownIdentitySecurityTests
{
    private readonly ProfileId _victimProfileId = new ProfileId("victim_profile_67890");
    private readonly ProfileId _attackerProfileId = new ProfileId("attacker_profile_12345");

    [Test]
    public void Attacker_WithNoSessionTicket_IsRejected()
    {
        // Attacker sets their declared ProfileId to victim's ProfileId, but has NO ticket
        bool resolved = TownSenderIdentityValidator.TryValidateSenderIdentity(
            _victimProfileId.Value,
            string.Empty,
            verifier: null,
            playerIdentifierForLogging: "PlayerRef 2",
            out ProfileId resolvedProfile);

        Assert.IsFalse(resolved, "Sender without session ticket must be rejected.");
        Assert.AreNotEqual(_victimProfileId, resolvedProfile);
    }

    [Test]
    public void Attacker_DeclaringVictimProfile_WithOwnTicket_IsRejectedAsSpoofing()
    {
        // Attacker generates/has a valid ticket for THEIR OWN profile
        string attackerOwnTicket = TownSessionTicketCodec.CreateTestTicket(_attackerProfileId, "AttackerName");

        // Attacker attempts to spoof: declares victim's profileId, but attaches own ticket
        bool resolved = TownSenderIdentityValidator.TryValidateSenderIdentity(
            _victimProfileId.Value,
            attackerOwnTicket,
            verifier: null,
            playerIdentifierForLogging: "PlayerRef 2",
            out ProfileId resolvedProfile);

        Assert.IsFalse(resolved, "Sender declaring a profile that does not match ticket claim must be rejected.");
        Assert.AreNotEqual(_victimProfileId, resolvedProfile);
    }

    [Test]
    public void Attacker_WithExpiredTicket_IsRejected()
    {
        // Create an expired ticket for victim (-100 seconds)
        string expiredTicket = TownSessionTicketCodec.CreateTestTicket(_victimProfileId, "VictimName", ttlSeconds: -100);

        bool resolved = TownSenderIdentityValidator.TryValidateSenderIdentity(
            _victimProfileId.Value,
            expiredTicket,
            verifier: null,
            playerIdentifierForLogging: "PlayerRef 2",
            out ProfileId resolvedProfile);

        Assert.IsFalse(resolved, "Expired ticket must be rejected.");
        Assert.AreNotEqual(_victimProfileId, resolvedProfile);
    }

    [Test]
    public void Attacker_WithWrongSessionScopeTicket_IsRejected()
    {
        // Create a ticket with wrong session ("RaidSession-123" instead of "Lobby-Town")
        string wrongScopeTicket = TownSessionTicketCodec.CreateTestTicket(_victimProfileId, "VictimName", sessionName: "RaidSession-123");

        bool resolved = TownSenderIdentityValidator.TryValidateSenderIdentity(
            _victimProfileId.Value,
            wrongScopeTicket,
            verifier: null,
            playerIdentifierForLogging: "PlayerRef 2",
            out ProfileId resolvedProfile);

        Assert.IsFalse(resolved, "Ticket for wrong session scope must be rejected.");
        Assert.AreNotEqual(_victimProfileId, resolvedProfile);
    }

    [Test]
    public void Attacker_WithMalformedTicket_IsRejected()
    {
        bool resolved = TownSenderIdentityValidator.TryValidateSenderIdentity(
            _victimProfileId.Value,
            "not.a.valid.jwt.or.ticket",
            verifier: null,
            playerIdentifierForLogging: "PlayerRef 2",
            out ProfileId resolvedProfile);

        Assert.IsFalse(resolved, "Malformed ticket must be rejected.");
        Assert.AreNotEqual(_victimProfileId, resolvedProfile);
    }

    [Test]
    public void Attacker_WithForgedSignature_RejectedByVerifier()
    {
        string forgedTicket = TownSessionTicketCodec.CreateTestTicket(_victimProfileId, "VictimName");

        var mockVerifier = new MockTownSessionTicketVerifier();
        mockVerifier.RegisterTicket(forgedTicket, _victimProfileId, "VictimName", valid: false, error: "INVALID_SIGNATURE");

        bool resolved = TownSenderIdentityValidator.TryValidateSenderIdentity(
            _victimProfileId.Value,
            forgedTicket,
            mockVerifier,
            playerIdentifierForLogging: "PlayerRef 2",
            out ProfileId resolvedProfile);

        Assert.IsFalse(resolved, "Ticket with invalid signature rejected by verifier must be rejected.");
        Assert.AreNotEqual(_victimProfileId, resolvedProfile);
    }

    [Test]
    public void LegitimatePlayer_WithAuthenticTicket_ResolvesSuccessfully()
    {
        string validTicket = TownSessionTicketCodec.CreateTestTicket(_victimProfileId, "VictimName");

        bool resolved = TownSenderIdentityValidator.TryValidateSenderIdentity(
            _victimProfileId.Value,
            validTicket,
            verifier: null,
            playerIdentifierForLogging: "PlayerRef 1",
            out ProfileId resolvedProfile);

        Assert.IsTrue(resolved, "Authentic ticket matching declared ProfileId must resolve successfully.");
        Assert.AreEqual(_victimProfileId, resolvedProfile);
    }

    [Test]
    public void LegitimatePlayer_WithVerifierValidation_ResolvesSuccessfully()
    {
        string validTicket = TownSessionTicketCodec.CreateTestTicket(_victimProfileId, "VictimName");

        var mockVerifier = new MockTownSessionTicketVerifier();
        mockVerifier.RegisterTicket(validTicket, _victimProfileId, "VictimName", valid: true);

        bool resolved = TownSenderIdentityValidator.TryValidateSenderIdentity(
            _victimProfileId.Value,
            validTicket,
            mockVerifier,
            playerIdentifierForLogging: "PlayerRef 1",
            out ProfileId resolvedProfile);

        Assert.IsTrue(resolved, "Authentic ticket approved by verifier must resolve successfully.");
        Assert.AreEqual(_victimProfileId, resolvedProfile);
    }
}
