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
#endif
