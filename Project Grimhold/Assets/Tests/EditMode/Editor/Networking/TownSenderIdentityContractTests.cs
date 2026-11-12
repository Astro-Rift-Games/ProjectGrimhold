using System;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using LogType = UnityEngine.LogType;

/// <summary>
/// FIX-54: Town credentials are never replicated; they travel by a targeted RPC whose sender is the
/// physical RPC source, and the runner-scoped context binds them per PlayerRef.
/// </summary>
[TestFixture]
public class TownSenderIdentityContractTests
{
    private static readonly PlayerRef Victim = PlayerRef.FromIndex(1);
    private static readonly PlayerRef Impostor = PlayerRef.FromIndex(2);
    private static readonly ProfileId VictimProfile = new ProfileId("victim_profile_67890");
    private static readonly Func<PlayerRef, bool> EveryoneActive = _ => true;

    private GameObject _runnerObject;
    private TownSenderIdentityContext _context;
    private string _victimTicket;
    private MockTownSessionTicketVerifier _verifier;

    [SetUp]
    public void SetUp()
    {
        _runnerObject = new GameObject("TownSenderIdentityContext");
        _context = _runnerObject.AddComponent<TownSenderIdentityContext>();
        _victimTicket = TownSessionTicketCodec.CreateTestTicket(VictimProfile, "Victim");
        _verifier = new MockTownSessionTicketVerifier();
        _verifier.RegisterTicket(_victimTicket, VictimProfile, "Victim");
        _context.TicketVerifier = _verifier;
    }

    [TearDown]
    public void TearDown()
    {
        if (_runnerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_runnerObject);
        }
    }

    [Test]
    public void SocialPlayerIdentity_DoesNotReplicateTicket()
    {
        Assert.IsNull(typeof(SocialPlayerIdentity).GetProperty("SessionTicket"));
        PropertyInfo[] properties = typeof(SocialPlayerIdentity).GetProperties(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        for (int index = 0; index < properties.Length; index++)
        {
            bool isNetworked = properties[index].GetCustomAttribute<NetworkedAttribute>() != null;
            Assert.IsFalse(isNetworked && properties[index].Name.Contains("Ticket"),
                $"{properties[index].Name} must not be replicated.");
        }
    }

    [Test]
    public void RegisterTownSenderRpc_IsStaticTargetedAndCarriesNoProfile()
    {
        MethodInfo method = typeof(SocialPlayerIdentity).GetMethod(
            "RPC_RegisterTownSender", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method);
        Assert.IsNotNull(method.GetCustomAttribute<RpcAttribute>());

        ParameterInfo[] parameters = method.GetParameters();
        bool hasTarget = false;
        for (int index = 0; index < parameters.Length; index++)
        {
            if (parameters[index].GetCustomAttribute<RpcTargetAttribute>() != null &&
                parameters[index].ParameterType == typeof(PlayerRef))
            {
                hasTarget = true;
            }

            Assert.AreNotEqual(typeof(ProfileId), parameters[index].ParameterType);
            Assert.IsFalse(parameters[index].Name.ToLowerInvariant().Contains("profile"));
        }

        Assert.IsTrue(hasTarget, "The ticket must be delivered only to the targeted authority.");
    }

    [Test]
    public void AcceptRegistration_BindsSenderToItsOwnTicket()
    {
        Assert.AreEqual(TownSenderRegistrationResult.Accepted,
            _context.AcceptRegistration(Victim, _victimTicket, EveryoneActive));

        Assert.IsTrue(_context.Registry.TryResolve(Victim, _verifier, out ProfileId resolved));
        Assert.AreEqual(VictimProfile, resolved);
    }

    [Test]
    public void AcceptRegistration_ImpostorWithBoundProfile_IsRejected()
    {
        _context.AcceptRegistration(Victim, _victimTicket, EveryoneActive);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[SECURITY\].*already bound"));

        TownSenderRegistrationResult result = _context.AcceptRegistration(Impostor, _victimTicket, EveryoneActive);

        Assert.AreEqual(TownSenderRegistrationResult.RejectedProfileBoundToAnotherSender, result);
        Assert.IsFalse(_context.Registry.TryResolve(Impostor, _verifier, out _));
        Assert.IsTrue(_context.Registry.TryResolve(Victim, _verifier, out _));
    }

    [Test]
    public void OnPlayerLeft_ReleasesBinding_SoReconnectingSenderMayClaimProfile()
    {
        _context.AcceptRegistration(Victim, _victimTicket, EveryoneActive);

        _context.OnPlayerLeft(null, Victim);

        Assert.IsFalse(_context.Registry.TryResolve(Victim, _verifier, out _));
        Assert.AreEqual(TownSenderRegistrationResult.Accepted,
            _context.AcceptRegistration(Impostor, _victimTicket, EveryoneActive));
    }
}
