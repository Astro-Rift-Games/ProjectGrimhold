using System.Threading.Tasks;
using Fusion;

/// <summary>Result of a productive Host handoff attempt (FIX-50).</summary>
public enum HostHandoffOutcome
{
    /// <summary>No handoff applies; the Host keeps the role.</summary>
    Stayed,
    /// <summary>Photon rejected the snapshot; the Host keeps the role so the room stays open.</summary>
    SnapshotRejected,
    /// <summary>Snapshot accepted and the runner shut down for Host Migration.</summary>
    HandedOff,
    /// <summary>No connected Raiding participant remains; the Raid closes through the normal path.</summary>
    CloseNormally
}

/// <summary>Runner surface a Host handoff needs; implemented over Fusion by <see cref="FusionHostHandoffRunner"/>.</summary>
public interface IHostHandoffRunner
{
    bool IsRunning { get; }
    bool IsServer { get; }
    bool RaidInProgress { get; }
    bool IsHostExitResolved { get; }
    int ConnectedRaidingRemoteParticipants { get; }
    Task<bool> PushSnapshotAsync();
    Task ShutdownAsync(ShutdownReason reason);
}

/// <summary>
/// Hands the Raid Host role to a remaining active participant. Fusion only migrates after an
/// accepted snapshot followed by a <see cref="ShutdownReason.HostMigration"/> shutdown; a plain
/// shutdown would close the room for everybody (FIX-48 report §1).
/// </summary>
public static class HostHandoffService
{
    /// <param name="beforeShutdown">Invoked after the snapshot is accepted and right before the
    /// HostMigration shutdown, so the caller can mark the shutdown as its own outgoing handoff.</param>
    public static async Task<HostHandoffOutcome> TryHandOffAsync(
        IHostHandoffRunner runner,
        System.Action beforeShutdown = null)
    {
        if (runner == null || !runner.IsRunning)
        {
            return HostHandoffOutcome.Stayed;
        }

        HostHandoffDecision decision = HostHandoffPolicy.Decide(
            runner.IsServer,
            runner.RaidInProgress,
            runner.IsHostExitResolved,
            runner.ConnectedRaidingRemoteParticipants);
        if (decision == HostHandoffDecision.Stay)
        {
            return HostHandoffOutcome.Stayed;
        }

        if (decision == HostHandoffDecision.CloseNormally)
        {
            return HostHandoffOutcome.CloseNormally;
        }

        // Without an accepted snapshot the cloud has nothing to resume, so the Host stays.
        if (!await runner.PushSnapshotAsync())
        {
            return HostHandoffOutcome.SnapshotRejected;
        }

        beforeShutdown?.Invoke();
        await runner.ShutdownAsync(ShutdownReason.HostMigration);
        return HostHandoffOutcome.HandedOff;
    }
}

/// <summary>Adapts a live Raid <see cref="NetworkRunner"/> to <see cref="IHostHandoffRunner"/>.</summary>
public sealed class FusionHostHandoffRunner : IHostHandoffRunner
{
    private readonly NetworkRunner _runner;
    private readonly NetworkSpawnManager _spawnManager;

    public FusionHostHandoffRunner(NetworkRunner runner)
    {
        _runner = runner;
        _spawnManager = runner != null ? runner.GetComponent<NetworkSpawnManager>() : null;
    }

    public bool IsRunning => _runner != null && _runner.IsRunning;
    public bool IsServer => _runner != null && _runner.IsServer;

    public bool RaidInProgress
    {
        get
        {
            NetworkMatchController matchController = _spawnManager != null
                ? _spawnManager.MatchController
                : null;
            return matchController != null &&
                   matchController.Phase == NetworkMatchController.MatchPhase.InProgress;
        }
    }

    public bool IsHostExitResolved =>
        _spawnManager != null && _spawnManager.IsLocalHostExitResolved();

    public int ConnectedRaidingRemoteParticipants =>
        _spawnManager != null ? _spawnManager.CountConnectedRaidingRemoteParticipants() : 0;

    public Task<bool> PushSnapshotAsync() => _runner.PushHostMigrationSnapshot();

    public Task ShutdownAsync(ShutdownReason reason) =>
        _runner.Shutdown(destroyGameObject: false, shutdownReason: reason);
}
