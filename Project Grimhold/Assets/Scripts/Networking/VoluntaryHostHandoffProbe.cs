#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading.Tasks;
using Fusion;

/// <summary>Result of a FIX-48 voluntary Host handoff probe attempt.</summary>
public enum VoluntaryHostHandoffOutcome
{
    Rejected,
    SnapshotRejected,
    HandedOff
}

/// <summary>Minimal runner surface the FIX-48 probe needs; implemented over Fusion by <see cref="FusionHostHandoffRunner"/>.</summary>
public interface IHostHandoffRunner
{
    bool IsRunning { get; }
    bool IsServer { get; }
    bool RaidInProgress { get; }
    Task<bool> PushSnapshotAsync();
    Task ShutdownAsync(ShutdownReason reason);
}

/// <summary>
/// Development-only FIX-48 probe. It verifies whether a Host leaving on purpose lets Photon
/// promote a remaining client: Fusion only migrates after an accepted snapshot and a
/// <see cref="ShutdownReason.HostMigration"/> shutdown, while a plain shutdown closes the room.
/// This is not the productive handoff (FIX-50) and bypasses no Return/Abandon rule.
/// </summary>
public static class VoluntaryHostHandoffProbe
{
    public static async Task<VoluntaryHostHandoffOutcome> RunAsync(IHostHandoffRunner runner)
    {
        if (runner == null || !runner.IsRunning || !runner.IsServer || !runner.RaidInProgress)
        {
            return VoluntaryHostHandoffOutcome.Rejected;
        }

        // Without an accepted snapshot the cloud has nothing to resume, so the Host stays.
        if (!await runner.PushSnapshotAsync())
        {
            return VoluntaryHostHandoffOutcome.SnapshotRejected;
        }

        await runner.ShutdownAsync(ShutdownReason.HostMigration);
        return VoluntaryHostHandoffOutcome.HandedOff;
    }
}

/// <summary>Adapts a live Raid <see cref="NetworkRunner"/> to the FIX-48 probe.</summary>
public sealed class FusionHostHandoffRunner : IHostHandoffRunner
{
    private readonly NetworkRunner _runner;

    public FusionHostHandoffRunner(NetworkRunner runner)
    {
        _runner = runner;
    }

    public bool IsRunning => _runner != null && _runner.IsRunning;
    public bool IsServer => _runner != null && _runner.IsServer;

    public bool RaidInProgress
    {
        get
        {
            NetworkSpawnManager spawnManager = _runner != null
                ? _runner.GetComponent<NetworkSpawnManager>()
                : null;
            NetworkMatchController matchController = spawnManager != null
                ? spawnManager.MatchController
                : null;
            return matchController != null &&
                   matchController.Phase == NetworkMatchController.MatchPhase.InProgress;
        }
    }

    public Task<bool> PushSnapshotAsync() => _runner.PushHostMigrationSnapshot();

    public Task ShutdownAsync(ShutdownReason reason) =>
        _runner.Shutdown(destroyGameObject: false, shutdownReason: reason);
}
#endif
