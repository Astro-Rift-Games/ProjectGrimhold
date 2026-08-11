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
        Closing,
        Finished
    }

    [Networked]
    public MatchPhase Phase { get; set; }

    [Networked]
    public int ExpectedAdmissionCount { get; private set; }

    [Networked]
    public NetworkString<_32> RaidGenerationId { get; private set; }

    [Networked]
    public RaidClosureState ClosureState { get; private set; }

    [Networked]
    public RaidClosureReason ClosureReason { get; private set; }

    [Networked]
    public int CleanupFailureCount { get; private set; }

    private bool _hasObservedParticipant;
    private bool _cleanupAttempted;

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
                ClosureState = RaidClosureState.None;
                CleanupFailureCount = 0;
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
    /// Initializes the runner-scoped generation identity before the first participant spawn.
    /// </summary>
    public void InitializeRaidGeneration(string raidGenerationId)
    {
        if (!HasStateAuthority || Phase != MatchPhase.WaitingForPlayers ||
            string.IsNullOrWhiteSpace(raidGenerationId))
        {
            return;
        }

        RaidGenerationId = raidGenerationId;
    }

    /// <summary>
    /// Requests a global raid cancellation. Only the Host/State Authority may invoke it.
    /// </summary>
    public bool TryCancelRaid()
    {
        if (!HasStateAuthority || Phase != MatchPhase.InProgress)
        {
            return false;
        }

        NetworkSpawnManager spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        if (spawnManager == null)
        {
            return false;
        }

        spawnManager.AbortRaidingParticipantsForClosure();
        BeginClosure(RaidClosureReason.HostCancellation);
        return true;
    }

    /// <summary>
    /// Configures a raid whose gameplay scene was loaded as part of StartGame.
    /// It closes admission only after the complete frozen manifest cohort has connected.
    /// </summary>
    public void ConfigurePreloadedRaidAdmission(int expectedAdmissionCount)
    {
        if (!HasStateAuthority || Phase != MatchPhase.WaitingForPlayers || expectedAdmissionCount <= 0)
        {
            return;
        }

        ExpectedAdmissionCount = expectedAdmissionCount;
    }

    /// <summary>
    /// Starts gameplay while keeping admission open for clients that know the raid code.
    /// The session closes through the normal authoritative cancellation or completion flow.
    /// </summary>
    public void ConfigureCodeRaidAdmission()
    {
        if (!HasStateAuthority || Phase != MatchPhase.WaitingForPlayers)
        {
            return;
        }

        ExpectedAdmissionCount = 0;
        Phase = MatchPhase.InProgress;
    }

    /// <summary>
    /// Determines whether every profile frozen into the Town launch manifest was admitted.
    /// There is intentionally no elapsed-time condition in this policy.
    /// </summary>
    public static bool IsExpectedCohortAdmitted(int expectedAdmissionCount, int admittedProfileCount) =>
        expectedAdmissionCount > 0 && admittedProfileCount >= expectedAdmissionCount;

    /// <summary>Returns whether late code-based admission is valid in the current match phase.</summary>
    public static bool IsCodeAdmissionOpen(bool allowsCodeAdmission, MatchPhase phase) =>
        allowsCodeAdmission && phase == MatchPhase.InProgress;

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (Phase == MatchPhase.InProgress)
        {
            NetworkSpawnManager activeSpawnManager = Runner.GetComponent<NetworkSpawnManager>();
            if (activeSpawnManager != null &&
                (activeSpawnManager.HasAdmittedRaidParticipants || _hasObservedParticipant))
            {
                _hasObservedParticipant |= activeSpawnManager.HasRaidingParticipants;
                if (_hasObservedParticipant && !activeSpawnManager.HasRaidingParticipants)
                {
                    BeginClosure(RaidClosureReason.NaturalCompletion);
                }
            }

            return;
        }

        if (Phase == MatchPhase.Closing)
        {
            AdvanceClosure();
            return;
        }

        if (Phase != MatchPhase.WaitingForPlayers || ExpectedAdmissionCount <= 0)
        {
            return;
        }

        NetworkSpawnManager spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        if (spawnManager != null &&
            IsExpectedCohortAdmitted(ExpectedAdmissionCount, spawnManager.AdmittedRaidProfileCount))
        {
            ClosePreloadedRaidAdmission();
        }
    }

    private void BeginClosure(RaidClosureReason reason)
    {
        if (Phase != MatchPhase.InProgress || !HasStateAuthority)
        {
            return;
        }

        Runner.SessionInfo.IsOpen = false;
        Runner.SessionInfo.IsVisible = false;
        ClosureReason = reason;
        ClosureState = RaidClosureState.AwaitingPersistence;
        Phase = MatchPhase.Closing;
        Debug.Log($"[NetworkMatchController] Raid closure started: {reason}.", this);
    }

    private void AdvanceClosure()
    {
        NetworkSpawnManager spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        if (spawnManager == null)
        {
            ClosureState = RaidClosureState.Failed;
            CleanupFailureCount++;
            return;
        }

        if (ClosureState == RaidClosureState.AwaitingPersistence)
        {
            if (spawnManager.HasPendingExtractionCommits)
            {
                return;
            }

            ClosureState = RaidClosureState.Cleaning;
        }

        if (ClosureState != RaidClosureState.Cleaning || _cleanupAttempted)
        {
            return;
        }

        _cleanupAttempted = true;
        bool cleanupSucceeded = spawnManager.TryCleanupRaidGeneration(out int failureCount);
        CleanupFailureCount = failureCount;
        ClosureState = RaidClosureState.ReturnOrdered;
        Phase = MatchPhase.Finished;
        ClosureState = RaidClosureState.Finished;
        if (!cleanupSucceeded)
        {
            Debug.LogError($"[NetworkMatchController] Raid cleanup completed with {failureCount} failure(s); return still ordered.", this);
        }
        else
        {
            Debug.Log("[NetworkMatchController] Raid cleanup completed; return ordered.", this);
        }
    }

    private void ClosePreloadedRaidAdmission()
    {
        Runner.SessionInfo.IsOpen = false;
        Runner.SessionInfo.IsVisible = false;
        Phase = MatchPhase.InProgress;
        Debug.Log("[NetworkMatchController] Preloaded raid admission closed.");
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

/// <summary>Authoritative progress of a raid generation after admission closes.</summary>
public enum RaidClosureState : byte
{
    None = 0,
    AwaitingPersistence = 1,
    Cleaning = 2,
    ReturnOrdered = 3,
    Finished = 4,
    Failed = 5
}

/// <summary>Reason that State Authority started closing the raid generation.</summary>
public enum RaidClosureReason : byte
{
    NaturalCompletion = 0,
    HostCancellation = 1
}
