#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

/// <summary>
/// FIX-48 probe: a voluntary Host exit may only request Host Migration after Photon accepted
/// a fresh snapshot, otherwise clients would wait for a migration the cloud cannot perform.
/// </summary>
public sealed class VoluntaryHostHandoffProbeTests
{
    private sealed class RecordingRunner : IHostHandoffRunner
    {
        public readonly List<string> Calls = new List<string>();
        public bool IsRunning { get; set; } = true;
        public bool IsServer { get; set; } = true;
        public bool RaidInProgress { get; set; } = true;
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
    public async Task RunAsync_ActiveRaidHost_PushesSnapshotThenShutsDownWithHostMigration()
    {
        var runner = new RecordingRunner();

        VoluntaryHostHandoffOutcome outcome = await VoluntaryHostHandoffProbe.RunAsync(runner);

        Assert.That(outcome, Is.EqualTo(VoluntaryHostHandoffOutcome.HandedOff));
        Assert.That(runner.Calls, Is.EqualTo(new[] { "push", "shutdown:" + ShutdownReason.HostMigration }));
    }

    [Test]
    public async Task RunAsync_SnapshotRejected_KeepsHostRunning()
    {
        var runner = new RecordingRunner { PushResult = false };

        VoluntaryHostHandoffOutcome outcome = await VoluntaryHostHandoffProbe.RunAsync(runner);

        Assert.That(outcome, Is.EqualTo(VoluntaryHostHandoffOutcome.SnapshotRejected));
        Assert.That(runner.Calls, Is.EqualTo(new[] { "push" }));
    }

    [Test]
    public async Task RunAsync_NullRunner_IsRejected()
    {
        VoluntaryHostHandoffOutcome outcome = await VoluntaryHostHandoffProbe.RunAsync(null);

        Assert.That(outcome, Is.EqualTo(VoluntaryHostHandoffOutcome.Rejected));
    }

    [TestCase(false, true, true)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public async Task RunAsync_NotAnActiveRaidHost_IsRejectedWithoutSideEffects(
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

        VoluntaryHostHandoffOutcome outcome = await VoluntaryHostHandoffProbe.RunAsync(runner);

        Assert.That(outcome, Is.EqualTo(VoluntaryHostHandoffOutcome.Rejected));
        Assert.That(runner.Calls, Is.Empty);
    }
}
#endif
