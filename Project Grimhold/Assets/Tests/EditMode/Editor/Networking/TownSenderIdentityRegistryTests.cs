using System;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using LogType = UnityEngine.LogType;

/// <summary>
/// FIX-54: Town identity is bound to the PlayerRef that delivered the ticket to the Town State Authority,
/// never to a self-declared or replicated identity.
/// </summary>
[TestFixture]
public class TownSenderIdentityRegistryTests
{
    private static readonly PlayerRef Victim = PlayerRef.FromIndex(1);
    private static readonly PlayerRef Impostor = PlayerRef.FromIndex(2);
    private static readonly ProfileId VictimProfile = new ProfileId("victim_profile_67890");
    private static readonly ProfileId ImpostorProfile = new ProfileId("attacker_profile_12345");
    private static readonly Func<PlayerRef, bool> EveryoneActive = _ => true;

    private string _victimTicket;
    private string _impostorTicket;
    private MockTownSessionTicketVerifier _verifier;
    private TownSenderIdentityRegistry _registry;

    [SetUp]
    public void SetUp()
    {
        _victimTicket = TownSessionTicketCodec.CreateTestTicket(VictimProfile, "Victim");
        _impostorTicket = TownSessionTicketCodec.CreateTestTicket(ImpostorProfile, "Impostor");
        _verifier = new MockTownSessionTicketVerifier();
        _verifier.RegisterTicket(_victimTicket, VictimProfile, "Victim");
        _verifier.RegisterTicket(_impostorTicket, ImpostorProfile, "Impostor");
        _registry = new TownSenderIdentityRegistry();
    }

    [Test]
    public void LegitimateSender_RegisteredWithOwnTicket_ResolvesOwnProfile()
    {
        Assert.AreEqual(TownSenderRegistrationResult.Accepted, _registry.Register(Victim, _victimTicket, _verifier, EveryoneActive));

        Assert.IsTrue(_registry.TryResolve(Victim, _verifier, out ProfileId resolved));
        Assert.AreEqual(VictimProfile, resolved);
        Assert.IsTrue(_registry.TryFindSender(VictimProfile, _verifier, out PlayerRef sender));
        Assert.AreEqual(Victim, sender);
    }

    [Test]
    public void Impostor_PresentingVictimTicket_WhileVictimIsBound_IsRejected_AndVictimBindingIsUnchanged()
    {
        _registry.Register(Victim, _victimTicket, _verifier, EveryoneActive);
        _registry.Register(Impostor, _impostorTicket, _verifier, EveryoneActive);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[SECURITY\].*already bound"));

        TownSenderRegistrationResult result = _registry.Register(Impostor, _victimTicket, _verifier, EveryoneActive);

        Assert.AreEqual(TownSenderRegistrationResult.RejectedSenderBoundToAnotherProfile, result);
        Assert.IsTrue(_registry.TryResolve(Impostor, _verifier, out ProfileId impostorResolved));
        Assert.AreEqual(ImpostorProfile, impostorResolved, "The impostor keeps only its own identity.");
        Assert.IsTrue(_registry.TryFindSender(VictimProfile, _verifier, out PlayerRef victimSender));
        Assert.AreEqual(Victim, victimSender, "Lookups for the victim must still route to the victim.");
    }

    [Test]
    public void Impostor_PresentingVictimTicket_AsFirstRegistration_WhileVictimIsBound_CannotResolveAnyProfile()
    {
        _registry.Register(Victim, _victimTicket, _verifier, EveryoneActive);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[SECURITY\].*already bound"));

        TownSenderRegistrationResult result = _registry.Register(Impostor, _victimTicket, _verifier, EveryoneActive);

        Assert.AreEqual(TownSenderRegistrationResult.RejectedProfileBoundToAnotherSender, result);
        Assert.IsFalse(_registry.TryResolve(Impostor, _verifier, out ProfileId resolved));
        Assert.IsTrue(_registry.TryFindSender(VictimProfile, _verifier, out PlayerRef victimSender));
        Assert.AreEqual(Victim, victimSender);
        Assert.AreEqual(default(ProfileId), resolved);
    }

    [Test]
    public void UnregisteredSender_IsRejected_RegardlessOfAnyDeclaredIdentity()
    {
        _registry.Register(Victim, _victimTicket, _verifier, EveryoneActive);

        Assert.IsFalse(_registry.TryResolve(Impostor, _verifier, out ProfileId resolved));
        Assert.AreEqual(default(ProfileId), resolved);
    }

    [Test]
    public void BoundSender_SwitchingToAnotherProfile_IsRejected_AndKeepsOriginalBinding()
    {
        _registry.Register(Impostor, _impostorTicket, _verifier, EveryoneActive);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[SECURITY\].*already bound"));

        TownSenderRegistrationResult result = _registry.Register(Impostor, _victimTicket, _verifier, _ => false);

        Assert.AreEqual(TownSenderRegistrationResult.RejectedSenderBoundToAnotherProfile, result);
        Assert.IsTrue(_registry.TryResolve(Impostor, _verifier, out ProfileId resolved));
        Assert.AreEqual(ImpostorProfile, resolved);
    }

    [Test]
    public void BoundSender_RefreshingItsOwnTicket_IsAccepted()
    {
        _registry.Register(Victim, _victimTicket, _verifier, EveryoneActive);
        string refreshed = TownSessionTicketCodec.CreateTestTicket(VictimProfile, "Victim", ttlSeconds: 3600);
        _verifier.RegisterTicket(refreshed, VictimProfile, "Victim");

        Assert.AreEqual(TownSenderRegistrationResult.Accepted, _registry.Register(Victim, refreshed, _verifier, EveryoneActive));
        Assert.IsTrue(_registry.TryResolve(Victim, _verifier, out ProfileId resolved));
        Assert.AreEqual(VictimProfile, resolved);
    }

    [Test]
    public void ProfileHeldByInactiveSender_CanBeBoundByReconnectingSender()
    {
        _registry.Register(Victim, _victimTicket, _verifier, EveryoneActive);
        PlayerRef reconnected = PlayerRef.FromIndex(3);

        TownSenderRegistrationResult result = _registry.Register(reconnected, _victimTicket, _verifier, player => player != Victim);

        Assert.AreEqual(TownSenderRegistrationResult.Accepted, result);
        Assert.IsFalse(_registry.TryResolve(Victim, _verifier, out _), "The stale binding is released.");
        Assert.IsTrue(_registry.TryResolve(reconnected, _verifier, out ProfileId resolved));
        Assert.AreEqual(VictimProfile, resolved);
    }

    [Test]
    public void RemovedSender_ReleasesItsProfile()
    {
        _registry.Register(Victim, _victimTicket, _verifier, EveryoneActive);

        Assert.IsTrue(_registry.Remove(Victim));

        Assert.IsFalse(_registry.TryResolve(Victim, _verifier, out _));
        Assert.IsFalse(_registry.TryFindSender(VictimProfile, _verifier, out _));
        Assert.AreEqual(TownSenderRegistrationResult.Accepted, _registry.Register(Impostor, _victimTicket, _verifier, EveryoneActive));
    }

    [Test]
    public void UnparseableTicket_IsRejectedAtRegistration()
    {
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[SECURITY\].*unparseable"));

        Assert.AreEqual(TownSenderRegistrationResult.RejectedInvalidTicket, _registry.Register(Victim, "not-a-ticket", _verifier, EveryoneActive));
        Assert.IsFalse(_registry.TryResolve(Victim, _verifier, out _));
    }

    [Test]
    public void RegisteredTicket_RejectedByVerifier_DoesNotResolve()
    {
        string forged = TownSessionTicketCodec.CreateTestTicket(VictimProfile, "Victim", ttlSeconds: 900);
        _verifier.RegisterTicket(forged, VictimProfile, "Victim", valid: false, error: "INVALID_SIGNATURE");
        _registry.Register(Impostor, forged, _verifier, EveryoneActive);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[SECURITY\].*Verifier rejected"));

        Assert.IsFalse(_registry.TryResolve(Impostor, _verifier, out ProfileId resolved));
        Assert.AreEqual(default(ProfileId), resolved);
    }

    [Test]
    public void Clear_ReleasesEveryBinding()
    {
        _registry.Register(Victim, _victimTicket, _verifier, EveryoneActive);

        _registry.Clear();

        Assert.IsFalse(_registry.TryResolve(Victim, _verifier, out _));
    }

    [Test]
    public void ForgedTicketRegisteredFirst_DoesNotLockOutTheRealOwner()
    {
        string forged = TownSessionTicketCodec.CreateTestTicket(VictimProfile, "Victim", ttlSeconds: 1200);
        _verifier.RegisterTicket(forged, VictimProfile, "Victim", valid: false, error: "INVALID_SIGNATURE");
        Assert.AreEqual(TownSenderRegistrationResult.Accepted, _registry.Register(Impostor, forged, _verifier, EveryoneActive));

        TownSenderRegistrationResult result = _registry.Register(Victim, _victimTicket, _verifier, EveryoneActive);

        Assert.AreEqual(TownSenderRegistrationResult.Accepted, result);
        Assert.IsTrue(_registry.TryResolve(Victim, _verifier, out ProfileId resolved));
        Assert.AreEqual(VictimProfile, resolved);
        Assert.IsTrue(_registry.TryFindSender(VictimProfile, _verifier, out PlayerRef sender));
        Assert.AreEqual(Victim, sender);
        Assert.IsFalse(_registry.TryResolve(Impostor, _verifier, out _));
    }

    [Test]
    public void PendingForgedTicketRegisteredFirst_DoesNotLockOutTheRealOwner_OnceItIsRejected()
    {
        string forged = TownSessionTicketCodec.CreateTestTicket(VictimProfile, "Victim", ttlSeconds: 1200);
        var verifier = new ControllableVerifier();
        verifier.Complete(_victimTicket, true, VictimProfile);
        _registry.Register(Impostor, forged, verifier, EveryoneActive);

        Assert.AreEqual(TownSenderRegistrationResult.Accepted, _registry.Register(Victim, _victimTicket, verifier, EveryoneActive));
        verifier.Complete(forged, false, VictimProfile);

        Assert.IsTrue(_registry.TryResolve(Victim, verifier, out ProfileId resolved));
        Assert.AreEqual(VictimProfile, resolved);
        Assert.IsFalse(_registry.TryResolve(Impostor, verifier, out _));
    }

    [Test]
    public void TwoGenuineClaimsOfOneProfile_OnlyTheEarliestRegistrationResolves()
    {
        var verifier = new ControllableVerifier();
        _registry.Register(Victim, _victimTicket, verifier, EveryoneActive);
        Assert.AreEqual(TownSenderRegistrationResult.Accepted, _registry.Register(Impostor, _victimTicket, verifier, EveryoneActive),
            "While the first claim is still pending the second is kept only as a candidate.");
        verifier.Complete(_victimTicket, true, VictimProfile);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[SECURITY\].*earlier"));

        Assert.IsFalse(_registry.TryResolve(Impostor, verifier, out ProfileId impostorResolved));
        Assert.AreEqual(default(ProfileId), impostorResolved);
        Assert.IsTrue(_registry.TryResolve(Victim, verifier, out ProfileId resolved));
        Assert.AreEqual(VictimProfile, resolved);
        Assert.IsTrue(_registry.TryFindSender(VictimProfile, verifier, out PlayerRef sender));
        Assert.AreEqual(Victim, sender);
    }

    private sealed class ControllableVerifier : ITownSessionTicketVerifier
    {
        private readonly System.Collections.Generic.Dictionary<string, System.Threading.Tasks.TaskCompletionSource<(bool valid, ProfileId profileId, string characterName, string error)>> _pending =
            new System.Collections.Generic.Dictionary<string, System.Threading.Tasks.TaskCompletionSource<(bool, ProfileId, string, string)>>(StringComparer.Ordinal);

        public void Complete(string rawTicket, bool valid, ProfileId profileId) =>
            Source(rawTicket).TrySetResult((valid, valid ? profileId : default, string.Empty, valid ? null : "INVALID_SIGNATURE"));

        public System.Threading.Tasks.Task<(bool valid, ProfileId profileId, string characterName, string error)> VerifyTownTicketAsync(string rawTicket) =>
            Source(rawTicket).Task;

        private System.Threading.Tasks.TaskCompletionSource<(bool, ProfileId, string, string)> Source(string rawTicket)
        {
            if (!_pending.TryGetValue(rawTicket, out var source))
            {
                source = new System.Threading.Tasks.TaskCompletionSource<(bool, ProfileId, string, string)>();
                _pending[rawTicket] = source;
            }

            return source;
        }
    }
}
