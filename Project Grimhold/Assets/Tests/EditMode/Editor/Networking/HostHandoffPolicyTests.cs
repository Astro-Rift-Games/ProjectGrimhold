#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

/// <summary>
/// FIX-50: once the Raid Host has resolved its own participation, the Host role passes to a
/// connected Raiding participant; with nobody active left the Raid closes normally.
/// </summary>
public sealed class HostHandoffPolicyTests
{
    [Test]
    public void ResolvedHostExit_WithOneConnectedRaidingParticipant_HandsOff()
    {
        Assert.That(
            HostHandoffPolicy.Decide(
                isServer: true,
                isRaidInProgress: true,
                isHostExitResolved: true,
                connectedRaidingRemoteParticipants: 1),
            Is.EqualTo(HostHandoffDecision.HandOff));
    }

    [Test]
    public void ResolvedHostExit_WithSeveralConnectedRaidingParticipants_HandsOff()
    {
        Assert.That(
            HostHandoffPolicy.Decide(true, true, true, 2),
            Is.EqualTo(HostHandoffDecision.HandOff));
    }

    [Test]
    public void ResolvedHostExit_WithNoConnectedRaidingParticipant_ClosesNormally()
    {
        Assert.That(
            HostHandoffPolicy.Decide(true, true, true, 0),
            Is.EqualTo(HostHandoffDecision.CloseNormally));
    }

    [Test]
    public void UnresolvedHostExit_NeverHandsOff()
    {
        Assert.That(
            HostHandoffPolicy.Decide(true, true, false, 1),
            Is.EqualTo(HostHandoffDecision.Stay));
    }

    [Test]
    public void Client_NeverHandsOff()
    {
        Assert.That(
            HostHandoffPolicy.Decide(false, true, true, 1),
            Is.EqualTo(HostHandoffDecision.Stay));
    }

    [Test]
    public void RaidNotInProgress_NeverHandsOff()
    {
        // Out of scope (S9): no migration outside the Dungeon.
        Assert.That(
            HostHandoffPolicy.Decide(true, false, true, 1),
            Is.EqualTo(HostHandoffDecision.Stay));
    }

    [Test]
    public void OperationalHost_MayResolveOwnExit_DuringActiveRaid()
    {
        // S2: the outgoing Host resolves Abandon/Return like any participant before handing off.
        Assert.That(
            HostHandoffPolicy.AcceptsExitRequest(requesterIsHost: true, isRaidInProgress: true),
            Is.True);
    }

    [Test]
    public void OperationalHost_ExitRequest_OutsideActiveRaid_StaysRejected()
    {
        // After the Raid finished the Host keeps the existing Results return path.
        Assert.That(
            HostHandoffPolicy.AcceptsExitRequest(requesterIsHost: true, isRaidInProgress: false),
            Is.False);
    }

    [Test]
    public void ReturnAuthorizedHost_ExitIsResolved()
    {
        // S4 (b)/(c): Return after Defeat or a completed extraction is authorized.
        Assert.That(
            HostHandoffPolicy.IsHostExitResolved(
                RaidParticipantState.Extracted,
                ExpeditionProgressionFinalizationCause.ExtractionConfirmed,
                isReturnAuthorized: true,
                hasProgressionResultSnapshot: true),
            Is.True);
    }

    [Test]
    public void ConfirmedAbandonWithResult_ExitIsResolved()
    {
        // S4 (a): the corpse exists and the abandon result was finalized.
        Assert.That(
            HostHandoffPolicy.IsHostExitResolved(
                RaidParticipantState.Aborted,
                ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed,
                isReturnAuthorized: false,
                hasProgressionResultSnapshot: true),
            Is.True);
    }

    [Test]
    public void ConfirmedAbandonWithoutResult_ExitIsNotResolvedYet()
    {
        Assert.That(
            HostHandoffPolicy.IsHostExitResolved(
                RaidParticipantState.Aborted,
                ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed,
                isReturnAuthorized: false,
                hasProgressionResultSnapshot: false),
            Is.False);
    }

    [Test]
    public void RaidingHost_ExitIsNotResolved()
    {
        Assert.That(
            HostHandoffPolicy.IsHostExitResolved(
                RaidParticipantState.Raiding,
                default,
                isReturnAuthorized: false,
                hasProgressionResultSnapshot: false),
            Is.False);
    }

    [Test]
    public void DefeatedHostBeforeReturn_ExitIsNotResolved()
    {
        // A Defeated Host still watches Results until it requests Return.
        Assert.That(
            HostHandoffPolicy.IsHostExitResolved(
                RaidParticipantState.Defeated,
                ExpeditionProgressionFinalizationCause.DefeatConfirmed,
                isReturnAuthorized: false,
                hasProgressionResultSnapshot: true),
            Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Client_ExitRequest_IsAlwaysAcceptedByThisRule(bool isRaidInProgress)
    {
        Assert.That(
            HostHandoffPolicy.AcceptsExitRequest(requesterIsHost: false, isRaidInProgress),
            Is.True);
    }

    [Test]
    public void DurablyCommittedAbandon_FromOperationalHost_AuthorizesReturn()
    {
        Assert.IsTrue(HostHandoffPolicy.AuthorizesReturnOnResolvedAbandon(
            requestedByOperationalHost: true,
            isDurablyCommitted: true));
    }

    [Test]
    public void ResolvedButNotDurablyCommittedAbandon_FromOperationalHost_DoesNotAuthorizeReturn()
    {
        Assert.IsFalse(HostHandoffPolicy.AuthorizesReturnOnResolvedAbandon(
            requestedByOperationalHost: true,
            isDurablyCommitted: false));
    }

    [Test]
    public void ClientAbandon_NeverAuthorizesReturn()
    {
        Assert.IsFalse(HostHandoffPolicy.AuthorizesReturnOnResolvedAbandon(
            requestedByOperationalHost: false,
            isDurablyCommitted: true));
        Assert.IsFalse(HostHandoffPolicy.AuthorizesReturnOnResolvedAbandon(
            requestedByOperationalHost: false,
            isDurablyCommitted: false));
    }

    private sealed class OrderRunner : IHostHandoffRunner
    {
        private readonly System.Collections.Generic.List<string> _log;
        private readonly bool _snapshotAccepted;

        public OrderRunner(System.Collections.Generic.List<string> log, bool snapshotAccepted)
        {
            _log = log;
            _snapshotAccepted = snapshotAccepted;
        }

        public bool IsRunning => true;
        public bool IsServer => true;
        public bool RaidInProgress => true;
        public bool IsHostExitResolved => true;
        public int ConnectedRaidingRemoteParticipants => 1;

        public System.Threading.Tasks.Task<bool> PushSnapshotAsync()
        {
            _log.Add("snapshot");
            return System.Threading.Tasks.Task.FromResult(_snapshotAccepted);
        }

        public System.Threading.Tasks.Task ShutdownAsync(Fusion.ShutdownReason reason)
        {
            _log.Add("shutdown");
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }

    [Test]
    public void HandOff_InvokesBeforeShutdownAfterSnapshotAndBeforeShutdown()
    {
        var log = new System.Collections.Generic.List<string>();
        HostHandoffOutcome outcome = HostHandoffService
            .TryHandOffAsync(new OrderRunner(log, true), () => log.Add("marker"))
            .GetAwaiter().GetResult();

        Assert.That(outcome, Is.EqualTo(HostHandoffOutcome.HandedOff));
        Assert.That(log, Is.EqualTo(new[] { "snapshot", "marker", "shutdown" }));
    }

    [Test]
    public void RejectedSnapshot_NeverInvokesBeforeShutdown()
    {
        var log = new System.Collections.Generic.List<string>();
        HostHandoffOutcome outcome = HostHandoffService
            .TryHandOffAsync(new OrderRunner(log, false), () => log.Add("marker"))
            .GetAwaiter().GetResult();

        Assert.That(outcome, Is.EqualTo(HostHandoffOutcome.SnapshotRejected));
        Assert.That(log, Is.EqualTo(new[] { "snapshot" }));
    }

    [TestCase(NetworkMatchPhaseCase.InProgress, 1, true)]
    [TestCase(NetworkMatchPhaseCase.InProgress, 0, false)]
    [TestCase(NetworkMatchPhaseCase.Finished, 1, false)]
    [TestCase(NetworkMatchPhaseCase.Finished, 0, false)]
    public void HostReturn_RoutesThroughParticipantOnlyForActiveHandoff(
        NetworkMatchPhaseCase phase,
        int connectedRaiding,
        bool expected)
    {
        Assert.That(
            HostHandoffPolicy.ShouldRouteHostReturnThroughParticipant(
                phase == NetworkMatchPhaseCase.InProgress,
                connectedRaiding),
            Is.EqualTo(expected));
    }

    public enum NetworkMatchPhaseCase { InProgress, Finished }

    [TestCase(false, false, true, 5f, 5f, true)]
    [TestCase(true, false, true, 5f, 5f, false)]
    [TestCase(false, true, true, 5f, 5f, false)]
    [TestCase(false, false, false, 5f, 5f, false)]
    [TestCase(false, false, true, 4f, 5f, false)]
    public void HandoffAttempt_IsGatedByInFlightOperationStateAndRetryDelay(
        bool inFlight,
        bool operationActive,
        bool inRaid,
        float now,
        float notBefore,
        bool expected)
    {
        Assert.That(
            HostHandoffPolicy.ShouldAttemptHandoff(inFlight, operationActive, inRaid, now, notBefore),
            Is.EqualTo(expected));
    }

    /// <summary>
    /// Root cause of the Host being unable to leave (L13): a Host invoking an
    /// InputAuthority→StateAuthority RPC on its own participant gets RpcInfo.Source ==
    /// PlayerRef.None under the default RpcHostMode.SourceIsServer, so the requester check
    /// ("source is None") rejected every Host Return/Abandon. The compiled RPC contract must
    /// report the Host's local player as the source.
    /// </summary>
    [TestCase("RPC_RequestReturn")]
    [TestCase("RPC_RequestAbandon")]
    public void HostExitRpcs_ReportTheHostLocalPlayerAsSource(string rpcName)
    {
        System.Reflection.MethodInfo method = typeof(NetworkRaidParticipant).GetMethod(
            rpcName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, rpcName);

        var rpc = (Fusion.RpcAttribute)System.Attribute.GetCustomAttribute(
            method,
            typeof(Fusion.RpcAttribute));
        Assert.That(rpc, Is.Not.Null, rpcName);
        Assert.That(rpc.HostMode, Is.EqualTo(Fusion.RpcHostMode.SourceIsHostPlayer), rpcName);
    }
}
#endif
