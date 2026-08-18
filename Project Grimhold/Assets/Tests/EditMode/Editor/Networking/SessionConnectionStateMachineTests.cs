using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

public sealed class SessionConnectionStateMachineTests
{
    [Test]
    public void NormalCycle_TransitionsThroughTownRaidAndBack()
    {
        var stateMachine = new SessionConnectionStateMachine();

        Assert.That(stateMachine.TryTransition(SessionConnectionState.ConnectingTown), Is.True);
        Assert.That(stateMachine.TryTransition(SessionConnectionState.Town), Is.True);

        for (int cycle = 0; cycle < 3; cycle++)
        {
            Assert.That(stateMachine.TryTransition(SessionConnectionState.PreparingRaid), Is.True);
            Assert.That(stateMachine.TryTransition(SessionConnectionState.ConnectingRaid), Is.True);
            Assert.That(stateMachine.TryTransition(SessionConnectionState.Raid), Is.True);
            Assert.That(stateMachine.TryTransition(SessionConnectionState.ReturningTown), Is.True);
            Assert.That(stateMachine.TryTransition(SessionConnectionState.Town), Is.True);
        }
    }

    [Test]
    public void CompletedTownEntry_ClearsRaidScopedState()
    {
        var owner = new GameObject("Repeat lifecycle coordinator test");
        owner.AddComponent<HubSessionLauncher>();
        owner.AddComponent<FusionSessionLauncher>();
        SessionConnectionCoordinator coordinator = owner.AddComponent<SessionConnectionCoordinator>();
        var profile = new ProfileId("11111111111111111111111111111111");
        RaidCode.TryParse("123456", out RaidCode code);
        RaidLaunchContext.TryCreate(code, profile, new[] { profile }, profile, 7, out RaidLaunchContext context);
        var ticket = new RaidTransitionTicket(
            new RaidConnectionRequest(code, RaidConnectionRole.Host),
            new PendingLoadoutReservation("old-reservation", Array.Empty<StashItem>()),
            SessionConnectionState.Raid,
            context);

        try
        {
            SetPrivateField(coordinator, "_activeTicket", (RaidTransitionTicket?)ticket);
            SetPrivateField(coordinator, "_acknowledgedLaunchSequence", 7);
            SetPrivateField(coordinator, "_launchDispatchActive", true);
            SetPrivateField(coordinator, "_raidAdmissionConfirmed", true);
            SetPrivateField(coordinator, "_loadoutConfirmationPending", true);
            SetPrivateField(coordinator, "_raidClosureReturnStarted", true);
            SetPrivateField(coordinator, "_raidClosureHostShutdownAt", 10f);
            SetPrivateField(coordinator, "_pendingTransitionFailure", (SessionTransitionResult?)SessionTransitionResult.ConnectionFailed);

            InvokePrivateMethod(coordinator, "CompleteTownEntry", new object[] { null });

            Assert.That(coordinator.ActiveTicket, Is.Null);
            Assert.That(ReadPrivateField<int>(coordinator, "_acknowledgedLaunchSequence"), Is.Zero);
            Assert.That(ReadPrivateField<bool>(coordinator, "_launchDispatchActive"), Is.False);
            Assert.That(ReadPrivateField<bool>(coordinator, "_raidAdmissionConfirmed"), Is.False);
            Assert.That(ReadPrivateField<bool>(coordinator, "_loadoutConfirmationPending"), Is.False);
            Assert.That(ReadPrivateField<bool>(coordinator, "_raidClosureReturnStarted"), Is.False);
            Assert.That(ReadPrivateField<float>(coordinator, "_raidClosureHostShutdownAt"), Is.EqualTo(-1f));
            Assert.That(coordinator.TryConsumeLastTransitionFailure(out _), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void InvalidTransition_IsRejectedWithoutChangingState()
    {
        var stateMachine = new SessionConnectionStateMachine();

        Assert.That(stateMachine.TryTransition(SessionConnectionState.Raid), Is.False);
        Assert.That(stateMachine.State, Is.EqualTo(SessionConnectionState.MainMenu));
    }

    [Test]
    public void FailedState_CanRetryTownOrReturnToTown()
    {
        var stateMachine = new SessionConnectionStateMachine();
        Assert.That(stateMachine.TryTransition(SessionConnectionState.Failed), Is.True);

        Assert.That(
            SessionConnectionStateMachine.CanTransition(
                stateMachine.State,
                SessionConnectionState.ConnectingTown),
            Is.True);
        Assert.That(
            SessionConnectionStateMachine.CanTransition(
                stateMachine.State,
                SessionConnectionState.ReturningTown),
            Is.True);
    }

    [TestCase(null, RaidConnectionRole.Host, "session")]
    [TestCase("", RaidConnectionRole.Host, "session")]
    [TestCase("raid", RaidConnectionRole.None, "session")]
    [TestCase("raid", RaidConnectionRole.Client, "")]
    public void RaidRequest_InvalidFields_AreRejected(
        string raidId,
        RaidConnectionRole role,
        string sessionName)
    {
        var request = new RaidConnectionRequest(raidId, role, sessionName);

        Assert.That(request.IsValid, Is.False);
    }

    [Test]
    public void RaidTicket_WithState_PreservesIdentity()
    {
        var request = new RaidConnectionRequest("raid-1", RaidConnectionRole.Client, "session-1");
        var ticket = new RaidTransitionTicket(
            request,
            SessionConnectionState.PreparingRaid);

        RaidTransitionTicket updated = ticket.WithState(SessionConnectionState.ConnectingRaid);

        Assert.That(updated.Request, Is.EqualTo(request));
        Assert.That(updated.State, Is.EqualTo(SessionConnectionState.ConnectingRaid));
    }

    [Test]
    public void LaunchTicket_ValidationIsPureAndRequiresMatchingIdentity()
    {
        var host = new ProfileId("11111111111111111111111111111111");
        RaidCode.TryParse("123456", out RaidCode code);
        RaidLaunchContext.TryCreate(code, host, new List<ProfileId> { host }, host, 1, out RaidLaunchContext context);
        var reservation = new PendingLoadoutReservation(
            "reservation-1",
            new List<StashItem>());
        var valid = new RaidTransitionTicket(
            new RaidConnectionRequest(code, RaidConnectionRole.Host),
            reservation,
            SessionConnectionState.Town,
            context);
        var mismatched = new RaidTransitionTicket(
            new RaidConnectionRequest("other-raid", RaidConnectionRole.Host, code.SessionName),
            reservation,
            SessionConnectionState.Town,
            context);

        Assert.That(valid.IsValid, Is.True);
        Assert.That(mismatched.IsValid, Is.False);
    }

    [Test]
    public void FailedRaidConnection_CanRecoverTownOrEnterFailed()
    {
        var stateMachine = new SessionConnectionStateMachine(SessionConnectionState.Town);

        Assert.That(stateMachine.TryTransition(SessionConnectionState.PreparingRaid), Is.True);
        Assert.That(stateMachine.TryTransition(SessionConnectionState.ConnectingRaid), Is.True);
        Assert.That(stateMachine.TryTransition(SessionConnectionState.ReturningTown), Is.True);
        Assert.That(stateMachine.TryTransition(SessionConnectionState.Failed), Is.True);
    }

    [Test]
    public async System.Threading.Tasks.Task ConcurrentRequest_ReturnsBusyWithoutChangingState()
    {
        var owner = new GameObject("TASK-57 Coordinator Test");
        owner.AddComponent<HubSessionLauncher>();
        owner.AddComponent<FusionSessionLauncher>();
        SessionConnectionCoordinator coordinator = owner.AddComponent<SessionConnectionCoordinator>();
        SetPrivateField(coordinator, "_operationActive", true);

        try
        {
            SessionTransitionResult result =
                await coordinator.ConnectToTownAsync();

            Assert.That(result, Is.EqualTo(SessionTransitionResult.Busy));
            Assert.That(coordinator.State, Is.EqualTo(SessionConnectionState.MainMenu));
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void Launcher_IgnoresCleanupFromAnOlderRunnerIdentity()
    {
        var launcherObject = new GameObject("TASK-57 Launcher Test");
        var olderRunnerObject = new GameObject("Older Runner");
        var currentRunnerObject = new GameObject("Current Runner");
        FusionSessionLauncher launcher = launcherObject.AddComponent<FusionSessionLauncher>();
        NetworkRunner olderRunner = olderRunnerObject.AddComponent<NetworkRunner>();
        NetworkRunner currentRunner = currentRunnerObject.AddComponent<NetworkRunner>();
        SetPrivateField(launcher, "_runner", currentRunner);

        try
        {
            launcher.ClearReferencesOnShutdown(olderRunner);
            Assert.That(launcher.Runner, Is.SameAs(currentRunner));

            launcher.ClearReferencesOnShutdown(currentRunner);
            Assert.That(launcher.Runner, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(launcherObject);
            Object.DestroyImmediate(olderRunnerObject);
            Object.DestroyImmediate(currentRunnerObject);
        }
    }

    [Test]
    public async Task InitialSceneWait_CancellationAndOwnerDestructionCannotPollForever()
    {
        var runnerObject = new GameObject("Initial scene wait runner");
        NetworkRunner runner = runnerObject.AddComponent<NetworkRunner>();
        LauncherShutdownListener listener = runnerObject.AddComponent<LauncherShutdownListener>();
        listener.Initialize(runner, (_, _) => { });

        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await listener.WaitForInitialSceneAsync(cancellation.Token));
        }

        listener.Initialize(runner, (_, _) => { });
        Task<bool> destroyedOwnerWait = listener.WaitForInitialSceneAsync();
        Object.DestroyImmediate(listener);

        Assert.That(await destroyedOwnerWait, Is.False);
        Object.DestroyImmediate(runnerObject);
    }

    [Test]
    public void NewTownJoinContext_DoesNotInheritDestroyedRaidAdmission()
    {
        var oldRunnerObject = new GameObject("Old raid join context");
        var newRunnerObject = new GameObject("New Town join context");
        var profile = new ProfileId("22222222222222222222222222222222");
        RaidCode.TryParse("654321", out RaidCode code);
        var joinData = new PlayerJoinData(profile);
        var raidAdmission = new RaidAdmissionData(
            code,
            profile,
            "old-reservation",
            Array.Empty<LootEntry>());

        try
        {
            LocalPlayerJoinContext oldContext = oldRunnerObject.AddComponent<LocalPlayerJoinContext>();
            oldContext.Initialize(in joinData, in raidAdmission);
            Assert.That(oldContext.HasRaidAdmission, Is.True);

            Object.DestroyImmediate(oldRunnerObject);

            LocalPlayerJoinContext townContext = newRunnerObject.AddComponent<LocalPlayerJoinContext>();
            townContext.Initialize(in joinData);
            Assert.That(townContext.HasRaidAdmission, Is.False);
            Assert.That(townContext.JoinData.ProfileId, Is.EqualTo(profile));
            Assert.That(townContext.RaidAdmission.IsValid, Is.False);
        }
        finally
        {
            if (oldRunnerObject != null)
            {
                Object.DestroyImmediate(oldRunnerObject);
            }

            Object.DestroyImmediate(newRunnerObject);
        }
    }

    [TestCase(GameMode.Host, true, 0, true)]
    [TestCase(GameMode.Client, false, 1, true)]
    [TestCase(GameMode.Host, false, 0, false)]
    [TestCase(GameMode.Client, true, 0, false)]
    [TestCase(GameMode.Shared, false, 0, false)]
    public void HostMigrationRolePolicy_SeparatesHostRestoreFromClientAdoption(
        GameMode gameMode,
        bool isServer,
        int expectedRole,
        bool expectedSuccess)
    {
        bool success = HostMigrationLifecycleController.TryResolveReplacementRole(
            gameMode,
            isServer,
            out HostMigrationReplacementRole role);

        Assert.That(success, Is.EqualTo(expectedSuccess));
        if (!expectedSuccess)
        {
            return;
        }

        Assert.That(
            (int)role,
            Is.EqualTo(expectedRole));
    }

    [TestCase(RaidParticipantState.Raiding, false, false, false, true)]
    [TestCase(RaidParticipantState.Defeated, false, false, false, true)]
    [TestCase(RaidParticipantState.Extracted, false, false, false, false)]
    [TestCase(RaidParticipantState.Aborted, false, false, false, false)]
    [TestCase(RaidParticipantState.Defeated, true, false, false, false)]
    [TestCase(RaidParticipantState.Raiding, false, true, false, false)]
    [TestCase(RaidParticipantState.Raiding, false, false, true, false)]
    public void HostMigrationEligibility_UsesDurableProfileAndTerminalSemantics(
        RaidParticipantState state,
        bool isReturnAuthorized,
        bool terminalKnown,
        bool isOldHost,
        bool expected)
    {
        var profileId = new ProfileId("profile-survivor");
        var oldHostProfileId = isOldHost
            ? profileId
            : new ProfileId("profile-old-host");

        Assert.That(
            NetworkSpawnManager.IsHostMigrationRecoveryEligible(
                profileId,
                oldHostProfileId,
                state,
                isReturnAuthorized,
                terminalKnown),
            Is.EqualTo(expected));
    }

    [Test]
    public void HostMigrationSealing_InvalidatesRecoveredMappingMissingFromCurrentActivePlayers()
    {
        PlayerRef recoveredPlayer = PlayerRef.FromIndex(2);
        var activePlayersBeforeDisconnect = new HashSet<PlayerRef>
        {
            recoveredPlayer
        };
        Assert.That(
            NetworkSpawnManager.IsRecoveredMappingCurrent(
                recoveredPlayer,
                activePlayersBeforeDisconnect,
                playerObjectMatches: true),
            Is.True);

        var activePlayersAtFinalInvariant = new HashSet<PlayerRef>();
        Assert.That(
            NetworkSpawnManager.IsRecoveredMappingCurrent(
                recoveredPlayer,
                activePlayersAtFinalInvariant,
                playerObjectMatches: true),
            Is.False);
    }

    [Test]
    public void HostMigrationShutdown_DoesNotRouteToUnexpectedRecovery()
    {
        Assert.That(
            SessionConnectionCoordinator.ShouldRecoverRaidShutdown(
                operationActive: false,
                isQuitting: false,
                SessionConnectionState.Raid,
                ShutdownReason.HostMigration),
            Is.False);
        Assert.That(
            SessionConnectionCoordinator.ShouldRecoverRaidShutdown(
                operationActive: false,
                isQuitting: false,
                SessionConnectionState.Raid,
                ShutdownReason.Error),
            Is.True);
    }

    [Test]
    public void Coordinator_HostMigrationShutdownKeepsRaidState()
    {
        var owner = new GameObject("Migration Coordinator Test");
        owner.AddComponent<HubSessionLauncher>();
        owner.AddComponent<FusionSessionLauncher>();
        SessionConnectionCoordinator coordinator = owner.AddComponent<SessionConnectionCoordinator>();
        var runnerObject = new GameObject("Migrating Runner");
        NetworkRunner runner = runnerObject.AddComponent<NetworkRunner>();
        SessionConnectionStateMachine stateMachine =
            ReadPrivateField<SessionConnectionStateMachine>(coordinator, "_stateMachine");

        try
        {
            Assert.That(stateMachine.TryTransition(SessionConnectionState.ConnectingRaid), Is.True);
            Assert.That(stateMachine.TryTransition(SessionConnectionState.Raid), Is.True);

            InvokePrivateMethod(
                coordinator,
                "OnRaidRunnerShutdown",
                runner,
                ShutdownReason.HostMigration);

            Assert.That(coordinator.State, Is.EqualTo(SessionConnectionState.Raid));
            Assert.That(coordinator.IsTransitioning, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(runnerObject);
        }
    }

    [Test]
    public void Launcher_AdoptsMigratedRunnerAndReplacementShutdownListener()
    {
        var launcherObject = new GameObject("Migration Launcher Test");
        var sourceObject = new GameObject("Source Runner");
        var replacementObject = new GameObject("Replacement Runner");
        var matchObject = new GameObject("Restored Match Controller");
        FusionSessionLauncher launcher = launcherObject.AddComponent<FusionSessionLauncher>();
        NetworkRunner sourceRunner = sourceObject.AddComponent<NetworkRunner>();
        NetworkRunner replacementRunner = replacementObject.AddComponent<NetworkRunner>();
        NetworkSceneManagerDefault sceneManager =
            replacementObject.AddComponent<NetworkSceneManagerDefault>();
        NetworkSpawnManager spawnManager = replacementObject.AddComponent<NetworkSpawnManager>();
        ExtractionSanctuaryAssignmentService sanctuary =
            replacementObject.AddComponent<ExtractionSanctuaryAssignmentService>();
        HostMigrationLifecycleController migrationController =
            replacementObject.AddComponent<HostMigrationLifecycleController>();
        HostMigrationSnapshotRestorer restorer =
            replacementObject.AddComponent<HostMigrationSnapshotRestorer>();
        matchObject.AddComponent<NetworkObject>();
        NetworkMatchController matchController = matchObject.AddComponent<NetworkMatchController>();
        SetPrivateField(spawnManager, "_matchController", matchController);
        SetPrivateField(launcher, "_runner", sourceRunner);
        SetPrivateField(launcher, "_runnerObject", sourceObject);

        var composition = new NetworkRunnerFactory.RunnerComposition(
            replacementObject,
            replacementRunner,
            sceneManager,
            spawnManager,
            sanctuary,
            migrationController,
            restorer);

        try
        {
            Assert.That(launcher.TryBeginHostMigration(sourceRunner), Is.True);
            Assert.That(
                launcher.TryAdoptMigratedRunner(sourceRunner, in composition),
                Is.True);
            Assert.That(launcher.Runner, Is.SameAs(replacementRunner));
            Assert.That(launcher.MatchController, Is.SameAs(matchController));
            Assert.That(ReadPrivateField<NetworkRunner>(launcher, "_hostMigrationSourceRunner"), Is.Null);

            LauncherShutdownListener listener =
                ReadPrivateField<LauncherShutdownListener>(launcher, "_shutdownListener");
            Assert.That(listener, Is.Not.Null);
            Assert.That(
                ReadPrivateField<NetworkRunner>(listener, "_expectedRunner"),
                Is.SameAs(replacementRunner));
        }
        finally
        {
            Object.DestroyImmediate(launcherObject);
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(replacementObject);
            Object.DestroyImmediate(matchObject);
        }
    }

    [Test]
    public async Task HostMigrationCompletion_PendingDoesNotFailOrCompleteEarly()
    {
        CreateMigrationSpawnManager(
            "Pending Migration Completion",
            out GameObject owner,
            out NetworkSpawnManager spawnManager);

        try
        {
            Task<NetworkSpawnManager.HostMigrationCompletionResult> pending =
                spawnManager.WaitForHostMigrationCompletionAsync(TimeSpan.FromSeconds(5));

            Assert.That(pending.IsCompleted, Is.False);

            CompleteMigration(
                spawnManager,
                NetworkSpawnManager.HostMigrationCompletionStatus.Failure,
                "Test cleanup.");
            await pending;
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public async Task HostMigrationCompletion_SuccessIsOneShot()
    {
        CreateMigrationSpawnManager(
            "Successful Migration Completion",
            out GameObject owner,
            out NetworkSpawnManager spawnManager);

        try
        {
            Task<NetworkSpawnManager.HostMigrationCompletionResult> pending =
                spawnManager.WaitForHostMigrationCompletionAsync(TimeSpan.FromSeconds(5));
            CompleteMigration(
                spawnManager,
                NetworkSpawnManager.HostMigrationCompletionStatus.Success,
                "Runtime rebind completed.");
            CompleteMigration(
                spawnManager,
                NetworkSpawnManager.HostMigrationCompletionStatus.Failure,
                "Late failure must not replace success.");

            NetworkSpawnManager.HostMigrationCompletionResult result = await pending;
            Assert.That(result.Status,
                Is.EqualTo(NetworkSpawnManager.HostMigrationCompletionStatus.Success));
            Assert.That(result.Succeeded, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public async Task HostMigrationCompletion_FailureCompletesWithoutWaitingForTimeout()
    {
        CreateMigrationSpawnManager(
            "Failed Migration Completion",
            out GameObject owner,
            out NetworkSpawnManager spawnManager);

        try
        {
            Task<NetworkSpawnManager.HostMigrationCompletionResult> pending =
                spawnManager.WaitForHostMigrationCompletionAsync(TimeSpan.FromSeconds(5));
            CompleteMigration(
                spawnManager,
                NetworkSpawnManager.HostMigrationCompletionStatus.Failure,
                "Snapshot restore failed.");

            NetworkSpawnManager.HostMigrationCompletionResult result = await pending;
            Assert.That(result.Status,
                Is.EqualTo(NetworkSpawnManager.HostMigrationCompletionStatus.Failure));
            Assert.That(result.Succeeded, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public async Task HostMigrationCompletion_PendingTimesOut()
    {
        CreateMigrationSpawnManager(
            "Timed Out Migration Completion",
            out GameObject owner,
            out NetworkSpawnManager spawnManager);

        try
        {
            NetworkSpawnManager.HostMigrationCompletionResult result =
                await spawnManager.WaitForHostMigrationCompletionAsync(
                    TimeSpan.FromMilliseconds(50));

            Assert.That(result.Status,
                Is.EqualTo(NetworkSpawnManager.HostMigrationCompletionStatus.Timeout));
            Assert.That(result.Details, Does.Contain("SnapshotReported=False"));

            CompleteMigration(
                spawnManager,
                NetworkSpawnManager.HostMigrationCompletionStatus.Success,
                "Late success must not revive a timed-out migration.");
            NetworkSpawnManager.HostMigrationCompletionResult repeated =
                await spawnManager.WaitForHostMigrationCompletionAsync(
                    TimeSpan.FromSeconds(5));
            Assert.That(repeated.Status,
                Is.EqualTo(NetworkSpawnManager.HostMigrationCompletionStatus.Timeout));
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public async Task HostMigrationCompletion_AlreadySuccessfulReturnsImmediately()
    {
        CreateMigrationSpawnManager(
            "Immediate Migration Completion",
            out GameObject owner,
            out NetworkSpawnManager spawnManager);

        try
        {
            CompleteMigration(
                spawnManager,
                NetworkSpawnManager.HostMigrationCompletionStatus.Success,
                "Completed before lifecycle await.");

            Task<NetworkSpawnManager.HostMigrationCompletionResult> completed =
                spawnManager.WaitForHostMigrationCompletionAsync(TimeSpan.FromSeconds(5));

            Assert.That(completed.IsCompleted, Is.True);
            Assert.That((await completed).Succeeded, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    private static void CreateMigrationSpawnManager(
        string name,
        out GameObject owner,
        out NetworkSpawnManager spawnManager)
    {
        owner = new GameObject(name);
        NetworkRunner runner = owner.AddComponent<NetworkRunner>();
        spawnManager = owner.AddComponent<NetworkSpawnManager>();
        Assert.That(
            spawnManager.InitializeForRunner(
                runner,
                NetworkPrefabRef.Empty,
                default,
                Array.Empty<NetworkPrefabRef>(),
                SessionStartupContext.HostMigrationResume,
                default),
            Is.True);
    }

    private static void CompleteMigration(
        NetworkSpawnManager spawnManager,
        NetworkSpawnManager.HostMigrationCompletionStatus status,
        string details)
    {
        InvokePrivateMethod(
            spawnManager,
            "CompleteHostMigration",
            status,
            details);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static T ReadPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }

    private static object InvokePrivateMethod(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(target, arguments);
    }
}
