#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

/// <summary>
/// FIX-50: the productive handoff only hands the Host role over after the outgoing Host resolved
/// its own participation, Photon accepted a fresh snapshot, and an active connected peer remains.
/// </summary>
public sealed class HostHandoffServiceTests
{
    private sealed class RecordingRunner : IHostHandoffRunner
    {
        public readonly List<string> Calls = new List<string>();
        public bool IsRunning { get; set; } = true;
        public bool IsServer { get; set; } = true;
        public bool RaidInProgress { get; set; } = true;
        public bool IsHostExitResolved { get; set; } = true;
        public int ConnectedRaidingRemoteParticipants { get; set; } = 1;
        public bool PushResult { get; set; } = true;

        public Task<bool> PushSnapshotAsync()
        {
            Calls.Add("push");
            return Task.FromResult(PushResult);
        }

        public Task ShutdownAsync(ShutdownReason reason)
        {
            Calls.Add("shutdown:" + reason);
            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task ResolvedHostExit_WithActivePeer_PushesSnapshotThenShutsDownForMigration()
    {
        var runner = new RecordingRunner();

        HostHandoffOutcome outcome = await HostHandoffService.TryHandOffAsync(runner);

        Assert.That(outcome, Is.EqualTo(HostHandoffOutcome.HandedOff));
        Assert.That(
            runner.Calls,
            Is.EqualTo(new[] { "push", "shutdown:" + ShutdownReason.HostMigration }));
    }

    [Test]
    public async Task SnapshotRejected_KeepsHostRunningForTheOthers()
    {
        // R4: without an accepted snapshot the room must never close for the remaining players.
        var runner = new RecordingRunner { PushResult = false };

        HostHandoffOutcome outcome = await HostHandoffService.TryHandOffAsync(runner);

        Assert.That(outcome, Is.EqualTo(HostHandoffOutcome.SnapshotRejected));
        Assert.That(runner.Calls, Is.EqualTo(new[] { "push" }));
    }

    [Test]
    public async Task ResolvedHostExit_WithoutActivePeer_ClosesNormallyWithoutMigration()
    {
        // S7: nobody active is connected, so the Raid closes through the normal path.
        var runner = new RecordingRunner { ConnectedRaidingRemoteParticipants = 0 };

        HostHandoffOutcome outcome = await HostHandoffService.TryHandOffAsync(runner);

        Assert.That(outcome, Is.EqualTo(HostHandoffOutcome.CloseNormally));
        Assert.That(runner.Calls, Is.Empty);
    }

    [Test]
    public async Task UnresolvedHostExit_DoesNothing()
    {
        var runner = new RecordingRunner { IsHostExitResolved = false };

        HostHandoffOutcome outcome = await HostHandoffService.TryHandOffAsync(runner);

        Assert.That(outcome, Is.EqualTo(HostHandoffOutcome.Stayed));
        Assert.That(runner.Calls, Is.Empty);
    }

    [Test]
    public async Task NullRunner_DoesNothing()
    {
        HostHandoffOutcome outcome = await HostHandoffService.TryHandOffAsync(null);

        Assert.That(outcome, Is.EqualTo(HostHandoffOutcome.Stayed));
    }

    [TestCase(false, true, true)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public async Task NotAnActiveRaidHost_DoesNothing(
        bool isRunning,
        bool isServer,
        bool raidInProgress)
    {
        var runner = new RecordingRunner
        {
            IsRunning = isRunning,
            IsServer = isServer,
            RaidInProgress = raidInProgress
        };

        HostHandoffOutcome outcome = await HostHandoffService.TryHandOffAsync(runner);

        Assert.That(outcome, Is.EqualTo(HostHandoffOutcome.Stayed));
        Assert.That(runner.Calls, Is.Empty);
    }
}
#endif
