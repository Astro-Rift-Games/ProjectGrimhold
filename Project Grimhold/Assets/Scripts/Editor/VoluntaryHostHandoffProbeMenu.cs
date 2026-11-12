#if UNITY_EDITOR
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>FIX-48: triggers the voluntary Host handoff probe from the Editor that hosts the Raid.</summary>
internal static class VoluntaryHostHandoffProbeMenu
{
    private const string MenuPath = "Grimhold/Dev/FIX-48 Voluntary Host Handoff";

    [MenuItem(MenuPath, validate = true)]
    private static bool CanRun() => Application.isPlaying;

    [MenuItem(MenuPath)]
    private static async void Run()
    {
        FusionSessionLauncher host = null;
        foreach (FusionSessionLauncher launcher in Object.FindObjectsByType<FusionSessionLauncher>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (launcher.Runner != null && launcher.Runner.IsServer)
            {
                host = launcher;
                break;
            }
        }

        if (host == null)
        {
            Debug.LogWarning("[HM-MULTI] FIX-48 probe: no running Raid Host runner in this process.");
            return;
        }

        Debug.Log($"[HM-MULTI] FIX-48 probe: voluntary handoff requested by Host. " +
                  $"Players={host.Runner.SessionInfo.PlayerCount}, Tick={host.Runner.Tick}.");
        Stopwatch elapsed = Stopwatch.StartNew();
        VoluntaryHostHandoffOutcome outcome =
            await VoluntaryHostHandoffProbe.RunAsync(new FusionHostHandoffRunner(host.Runner));
        Debug.Log($"[HM-MULTI] FIX-48 probe: outcome={outcome}, elapsedMs={elapsed.ElapsedMilliseconds}.");
    }
}
#endif
