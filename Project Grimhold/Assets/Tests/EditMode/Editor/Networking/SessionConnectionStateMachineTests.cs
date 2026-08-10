using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

public sealed class SessionConnectionStateMachineTests
{
    [Test]
    public void NormalCycle_TransitionsThroughTownRaidAndBack()
    {
        var stateMachine = new SessionConnectionStateMachine();

        Assert.That(stateMachine.TryTransition(SessionConnectionState.ConnectingTown), Is.True);
        Assert.That(stateMachine.TryTransition(SessionConnectionState.Town), Is.True);
        Assert.That(stateMachine.TryTransition(SessionConnectionState.PreparingRaid), Is.True);
        Assert.That(stateMachine.TryTransition(SessionConnectionState.ConnectingRaid), Is.True);
        Assert.That(stateMachine.TryTransition(SessionConnectionState.Raid), Is.True);
        Assert.That(stateMachine.TryTransition(SessionConnectionState.ReturningTown), Is.True);
        Assert.That(stateMachine.TryTransition(SessionConnectionState.Town), Is.True);
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
    public void RaidTicket_WithState_PreservesIdentityAndBuild()
    {
        var request = new RaidConnectionRequest("raid-1", RaidConnectionRole.Client, "session-1");
        var ticket = new RaidTransitionTicket(
            request,
            PlayerClassId.Ranged,
            SessionConnectionState.PreparingRaid);

        RaidTransitionTicket updated = ticket.WithState(SessionConnectionState.ConnectingRaid);

        Assert.That(updated.Request, Is.EqualTo(request));
        Assert.That(updated.SelectedBuild, Is.EqualTo(PlayerClassId.Ranged));
        Assert.That(updated.State, Is.EqualTo(SessionConnectionState.ConnectingRaid));
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
                await coordinator.ConnectToTownAsync(PlayerClassId.Melee);

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

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
