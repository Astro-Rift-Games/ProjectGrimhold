using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

public sealed class RaidReconnectClientFlowTests
{
    [Test]
    public void RaidReconnectTicket_ValidInputs_ReportsValidAndGeneratesPayload()
    {
        Assert.That(RaidCode.TryParse("123456", out RaidCode raidCode), Is.True);
        var profileId = new ProfileId("11111111111111111111111111111111");
        string generationId = "raid-123456";
        string sessionName = "raid-123456";

        var ticket = new RaidReconnectTicket(raidCode, profileId, generationId, sessionName);

        Assert.That(ticket.IsValid, Is.True);
        Assert.That(ticket.RaidCode, Is.EqualTo(raidCode));
        Assert.That(ticket.ProfileId, Is.EqualTo(profileId));
        Assert.That(ticket.RaidGenerationId, Is.EqualTo(generationId));
        Assert.That(ticket.SessionName, Is.EqualTo(sessionName));

        RaidReconnectPayload payload = ticket.ToPayload();
        Assert.That(payload.IsValid, Is.True);
        Assert.That(payload.RaidCode, Is.EqualTo(raidCode));
        Assert.That(payload.ProfileId, Is.EqualTo(profileId));
        Assert.That(payload.RaidGenerationId, Is.EqualTo(generationId));
    }

    [Test]
    public void RaidReconnectTicket_InvalidInputs_ReportsInvalid()
    {
        Assert.That(RaidCode.TryParse("123456", out RaidCode raidCode), Is.True);
        var profileId = new ProfileId("11111111111111111111111111111111");

        var invalidGen = new RaidReconnectTicket(raidCode, profileId, null, "session");
        Assert.That(invalidGen.IsValid, Is.False);

        var invalidSession = new RaidReconnectTicket(raidCode, profileId, "raid-gen", string.Empty);
        Assert.That(invalidSession.IsValid, Is.False);

        var invalidCode = new RaidReconnectTicket(default, profileId, "raid-gen", "session");
        Assert.That(invalidCode.IsValid, Is.False);

        var invalidProfile = new RaidReconnectTicket(raidCode, default, "raid-gen", "session");
        Assert.That(invalidProfile.IsValid, Is.False);
    }

    [Test]
    public void RaidReconnectTicket_EqualityAndHashCode_FunctionCorrectly()
    {
        Assert.That(RaidCode.TryParse("123456", out RaidCode codeA), Is.True);
        Assert.That(RaidCode.TryParse("654321", out RaidCode codeB), Is.True);
        var profile = new ProfileId("11111111111111111111111111111111");

        var ticket1 = new RaidReconnectTicket(codeA, profile, "gen-1", "session-1");
        var ticket2 = new RaidReconnectTicket(codeA, profile, "gen-1", "session-1");
        var ticketDiffCode = new RaidReconnectTicket(codeB, profile, "gen-1", "session-1");

        Assert.That(ticket1, Is.EqualTo(ticket2));
        Assert.That(ticket1 == ticket2, Is.True);
        Assert.That(ticket1 != ticketDiffCode, Is.True);
        Assert.That(ticket1.GetHashCode(), Is.EqualTo(ticket2.GetHashCode()));
    }

    [Test]
    public void RaidReconnectPayloadCodec_EncodesAndDecodesTicketPayload()
    {
        Assert.That(RaidCode.TryParse("987654", out RaidCode raidCode), Is.True);
        var profileId = new ProfileId("22222222222222222222222222222222");
        string generationId = "raid-987654";

        var ticket = new RaidReconnectTicket(raidCode, profileId, generationId, "session-987654");
        RaidReconnectPayload payload = ticket.ToPayload();

        Assert.That(RaidReconnectPayloadCodec.TryEncode(payload, out byte[] token), Is.True);
        Assert.That(token, Is.Not.Null);
        Assert.That(token.Length, Is.GreaterThan(0));

        Assert.That(RaidReconnectPayloadCodec.TryDecode(token, out RaidReconnectPayload decoded), Is.True);
        Assert.That(decoded.IsValid, Is.True);
        Assert.That(decoded.RaidCode, Is.EqualTo(raidCode));
        Assert.That(decoded.ProfileId, Is.EqualTo(profileId));
        Assert.That(decoded.RaidGenerationId, Is.EqualTo(generationId));
    }

    [Test]
    public void SessionConnectionStateMachine_SupportsTownToConnectingRaid_ForReconnection()
    {
        var sm = new SessionConnectionStateMachine();

        Assert.That(sm.TryTransition(SessionConnectionState.ConnectingTown), Is.True);
        Assert.That(sm.TryTransition(SessionConnectionState.Town), Is.True);

        // Direct transition from Town to ConnectingRaid is required by client reconnection flow
        Assert.That(sm.TryTransition(SessionConnectionState.ConnectingRaid), Is.True);

        // Can succeed into Raid
        Assert.That(sm.TryTransition(SessionConnectionState.Raid), Is.True);

        // Can return to Town
        Assert.That(sm.TryTransition(SessionConnectionState.ReturningTown), Is.True);
        Assert.That(sm.TryTransition(SessionConnectionState.Town), Is.True);

        // Can transition to ConnectingRaid again and fail back to ReturningTown
        Assert.That(sm.TryTransition(SessionConnectionState.ConnectingRaid), Is.True);
        Assert.That(sm.TryTransition(SessionConnectionState.ReturningTown), Is.True);
        Assert.That(sm.TryTransition(SessionConnectionState.Town), Is.True);

        // Can transition to ConnectingRaid and fail to Failed
        Assert.That(sm.TryTransition(SessionConnectionState.ConnectingRaid), Is.True);
        Assert.That(sm.TryTransition(SessionConnectionState.Failed), Is.True);
    }

    [Test]
    public void SessionConnectionCoordinator_CompleteTownEntry_PreservesActiveReconnectTicket()
    {
        var owner = new GameObject("ReconnectCoordinatorTest");
        owner.AddComponent<HubSessionLauncher>();
        owner.AddComponent<FusionSessionLauncher>();
        SessionConnectionCoordinator coordinator = owner.AddComponent<SessionConnectionCoordinator>();

        Assert.That(RaidCode.TryParse("123456", out RaidCode code), Is.True);
        var profile = new ProfileId("11111111111111111111111111111111");
        var ticket = new RaidReconnectTicket(code, profile, "raid-123456", "raid-123456");

        try
        {
            SetPrivateField(coordinator, "_activeReconnectTicket", (RaidReconnectTicket?)ticket);
            Assert.That(coordinator.HasActiveReconnectTicket, Is.True);

            // Invoke CompleteTownEntry via reflection
            MethodInfo completeTownEntryMethod = typeof(SessionConnectionCoordinator).GetMethod(
                "CompleteTownEntry",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(completeTownEntryMethod, Is.Not.Null);
            completeTownEntryMethod.Invoke(coordinator, new object[] { null });

            // Active ticket should remain preserved for player decision in Town
            Assert.That(coordinator.HasActiveReconnectTicket, Is.True);
            Assert.That(coordinator.ActiveReconnectTicket.Value, Is.EqualTo(ticket));
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [SetUp]
    [TearDown]
    public void CleanupStore()
    {
        RaidReconnectTicketStore.Clear();
    }

    [Test]
    public void RaidReconnectTicketStore_SaveLoadAndClear_PreservesTicketAcrossSessions()
    {
        Assert.That(RaidCode.TryParse("654321", out RaidCode code), Is.True);
        var profile = new ProfileId("persistent-raider-profile-1234");
        var ticket = new RaidReconnectTicket(code, profile, "gen-persistent", "session-persistent");

        RaidReconnectTicketStore.Save(ticket);

        Assert.That(RaidReconnectTicketStore.TryLoad(profile, out RaidReconnectTicket loadedTicket), Is.True);
        Assert.That(loadedTicket, Is.EqualTo(ticket));

        var differentProfile = new ProfileId("another-profile-5678");
        Assert.That(RaidReconnectTicketStore.TryLoad(differentProfile, out _), Is.False);

        RaidReconnectTicketStore.Clear();
        Assert.That(RaidReconnectTicketStore.TryLoad(profile, out _), Is.False);
    }

    [Test]
    public void SessionConnectionCoordinator_LoadsPersistedTicketFromStore_WhenMemoryEmpty()
    {
        ProfileId previousProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        var owner = new GameObject("PersistedTicketCoordinatorTest");
        owner.AddComponent<HubSessionLauncher>();
        owner.AddComponent<FusionSessionLauncher>();
        SessionConnectionCoordinator coordinator = owner.AddComponent<SessionConnectionCoordinator>();

        Assert.That(RaidCode.TryParse("987654", out RaidCode code), Is.True);
        var profile = new ProfileId("persisted-coordinator-raider");
        var ticket = new RaidReconnectTicket(code, profile, "raid-gen-persisted", "session-persisted");

        try
        {
            LocalProfileProvider.SetRemoteCharacterId(profile);
            RaidReconnectTicketStore.Save(ticket);

            Assert.That(coordinator.HasActiveReconnectTicket, Is.True);
            Assert.That(coordinator.ActiveReconnectTicket, Is.EqualTo(ticket));

            coordinator.DiscardActiveReconnectTicket();
            Assert.That(coordinator.HasActiveReconnectTicket, Is.False);
            Assert.That(RaidReconnectTicketStore.TryLoad(profile, out _), Is.False);
        }
        finally
        {
            if (previousProfile.IsValid)
            {
                LocalProfileProvider.SetRemoteCharacterId(previousProfile);
            }
            else
            {
                LocalProfileProvider.ClearRemoteCharacterId();
            }

            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void SessionConnectionCoordinator_DiscardActiveReconnectTicket_ClearsTicketAndInvokesEvent()
    {
        var owner = new GameObject("DiscardReconnectTest");
        owner.AddComponent<HubSessionLauncher>();
        owner.AddComponent<FusionSessionLauncher>();
        SessionConnectionCoordinator coordinator = owner.AddComponent<SessionConnectionCoordinator>();

        Assert.That(RaidCode.TryParse("123456", out RaidCode code), Is.True);
        var profile = new ProfileId("11111111111111111111111111111111");
        var ticket = new RaidReconnectTicket(code, profile, "raid-123456", "raid-123456");

        bool eventInvoked = false;
        RaidReconnectTicket? receivedTicket = ticket;
        coordinator.ReconnectTicketChanged += (t) =>
        {
            eventInvoked = true;
            receivedTicket = t;
        };

        try
        {
            SetPrivateField(coordinator, "_activeReconnectTicket", (RaidReconnectTicket?)ticket);
            Assert.That(coordinator.HasActiveReconnectTicket, Is.True);

            coordinator.DiscardActiveReconnectTicket();

            Assert.That(coordinator.HasActiveReconnectTicket, Is.False);
            Assert.That(eventInvoked, Is.True);
            Assert.That(receivedTicket.HasValue, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void HubRunnerFactory_TryCreate_DisablesSceneTakeOver_ForPristineTownScene()
    {
        var profile = new ProfileId("11111111111111111111111111111111");
        var joinData = new PlayerJoinData(profile);
        bool created = HubRunnerFactory.TryCreate(in joinData, default, out var composition);

        try
        {
            Assert.That(created, Is.True);
            Assert.That(composition.SceneManager, Is.Not.Null);
            Assert.That(composition.SceneManager.IsSceneTakeOverEnabled, Is.False);
        }
        finally
        {
            if (composition.RunnerObject != null)
            {
                Object.DestroyImmediate(composition.RunnerObject);
            }
        }
    }

    [Test]
    public void HubSpawnSceneConfiguration_TryGetSpawnPose_HandlesArbitraryPlayerIdsViaModulo()
    {
        var root = new GameObject("SpawnConfigTest");
        var config = root.AddComponent<HubSpawnSceneConfiguration>();
        var spawnPoints = new Transform[RaidSessionRules.MaxParticipants];
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var sp = new GameObject($"SpawnPoint_{i}");
            sp.transform.SetParent(root.transform);
            sp.transform.position = new Vector3(i * 10f, 0f, 0f);
            spawnPoints[i] = sp.transform;
        }

        SetPrivateField(config, "_spawnPoints", spawnPoints);

        try
        {
            Assert.That(config.SpawnPointCount, Is.EqualTo(RaidSessionRules.MaxParticipants));

            // Standard indices
            Assert.That(config.TryGetSpawnPose(0, out Vector3 pos0, out _), Is.True);
            Assert.That(pos0.x, Is.EqualTo(0f));

            Assert.That(config.TryGetSpawnPose(5, out Vector3 pos5, out _), Is.True);
            Assert.That(pos5.x, Is.EqualTo(50f));

            // Actor numbers exceeding spawn point count (e.g. Photon actor 16, 17, 33)
            Assert.That(config.TryGetSpawnPose(16, out Vector3 pos16, out _), Is.True);
            Assert.That(pos16.x, Is.EqualTo(0f)); // wrapped to 0

            Assert.That(config.TryGetSpawnPose(17, out Vector3 pos17, out _), Is.True);
            Assert.That(pos17.x, Is.EqualTo(10f)); // wrapped to 1

            Assert.That(config.TryGetSpawnPose(33, out Vector3 pos33, out _), Is.True);
            Assert.That(pos33.x, Is.EqualTo(10f)); // wrapped to 1
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }
}
