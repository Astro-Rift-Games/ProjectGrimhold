using Fusion;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Authoritative match coordinator that tracks the current game lifecycle phase.
/// State changes are initiated exclusively by the Host/Server.
/// This behaviour is spawned by Fusion and lives on the runner's network hierarchy.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkMatchController : NetworkBehaviour
{
    public enum MatchPhase
    {
        WaitingForPlayers,
        Starting,
        InProgress,
        Finished
    }

    [Networked]
    public MatchPhase Phase { get; set; }

    public override void Spawned()
    {
        var spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        if (spawnManager == null)
        {
            Debug.LogError("[NetworkMatchController] Spawned: NetworkSpawnManager is missing on the runner. This is a fatal composition error.");
            return;
        }

        // Auto-register to the local spawn manager
        spawnManager.BindMatchController(this);

        if (HasStateAuthority)
        {
            if (spawnManager.ShouldInitializeMatchPhase)
            {
                Phase = MatchPhase.WaitingForPlayers;
                Debug.Log("[NetworkMatchController] Spawned with StateAuthority. Phase initialized to WaitingForPlayers.");
            }
            else
            {
                Debug.Log($"[NetworkMatchController] Spawned with StateAuthority on HostMigrationResume. Phase untouched (currently {Phase}).");
            }
        }
        else
        {
            Debug.Log($"[NetworkMatchController] Spawned on Client. Current Phase: {Phase}.");
        }
    }

    /// <summary>
    /// Starts the game transition. Can only be invoked by the Host/Server.
    /// Closes the session and loads the gameplay scene.
    /// </summary>
    public async Task StartGameAsync(string gameplaySceneName)
    {
        if (!Runner.IsServer)
        {
            Debug.LogWarning("[NetworkMatchController] Only the Host can start the game.");
            return;
        }

        if (Phase != MatchPhase.WaitingForPlayers)
        {
            Debug.LogWarning($"[NetworkMatchController] Cannot start game from phase {Phase}.");
            return;
        }

        // Validate scene index
        int sceneBuildIndex = SceneUtility.GetBuildIndexByScenePath(gameplaySceneName);
        if (sceneBuildIndex < 0)
        {
            throw new ArgumentException($"[NetworkMatchController] Invalid scene name or index: {gameplaySceneName}");
        }

        // 1. Set phase to Starting
        Phase = MatchPhase.Starting;

        // 2. Set SessionInfo properties (Close and hide session)
        Runner.SessionInfo.IsOpen = false;
        Runner.SessionInfo.IsVisible = false;

        Debug.Log("[NetworkMatchController] Phase changed to Starting. Session closed & hidden.");

        try
        {
            // 3. Load the scene
            await Runner.LoadScene(
                SceneRef.FromIndex(sceneBuildIndex),
                LoadSceneMode.Single);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkMatchController] Failed to load scene: {ex.Message}");
            // Remain in Starting or keep session closed on failure. Do not advance phase.
            throw;
        }

        // 4. Change phase to InProgress
        Phase = MatchPhase.InProgress;
        Debug.Log("[NetworkMatchController] Phase changed to InProgress.");
    }
}
