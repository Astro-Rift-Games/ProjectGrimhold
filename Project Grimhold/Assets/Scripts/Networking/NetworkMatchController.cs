using Fusion;
using System;
using UnityEngine;

/// <summary>
/// Authoritative match coordinator that tracks the current game lifecycle phase.
/// State changes are initiated exclusively by the Host/Server.
/// This behaviour is spawned by Fusion and lives on the runner's network hierarchy.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkMatchController : NetworkBehaviour
{
    private const float ExpectedAdmissionTimeoutSeconds = 30f;
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
    public TickTimer ExpectedAdmissionDeadline { get; private set; }
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
        ExpectedAdmissionDeadline = TickTimer.CreateFromSeconds(Runner, ExpectedAdmissionTimeoutSeconds);
    }

    /// <summary>
    /// Determines whether every profile frozen in Town completed admission and player bootstrap.
    /// There is intentionally no elapsed-time condition in this policy.
    /// </summary>
    public static bool IsExpectedCohortAdmitted(int expectedAdmissionCount, int admittedProfileCount) =>
        expectedAdmissionCount > 0 && admittedProfileCount >= expectedAdmissionCount;
    /// <summary>
    /// Authoritatively starts the already-loaded raid and executes its one-time
    /// initial PvPvE bootstrap without reloading Gameplay.
    /// </summary>
    public bool TryStartRaid()
    {
        if (!HasStateAuthority || Phase != MatchPhase.WaitingForPlayers)
        {
            return false;
        }

        NetworkSpawnManager spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        if (spawnManager == null)
        {
            return false;
        }

        Phase = MatchPhase.Starting;
        Runner.SessionInfo.IsOpen = false;
        Runner.SessionInfo.IsVisible = false;

        if (!spawnManager.TryExecuteInitialRaidBootstrap(out string failure))
        {
            Debug.LogError($"[NetworkMatchController] Initial raid bootstrap failed: {failure}", this);
            spawnManager.AbortRaidingParticipantsForClosure();
            BeginClosure(RaidClosureReason.BootstrapFailure);
            return false;
        }

        Phase = MatchPhase.InProgress;
        Debug.Log("[NetworkMatchController] Raid started after successful initial bootstrap.", this);
        return true;
    }

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
            IsExpectedCohortAdmitted(ExpectedAdmissionCount, spawnManager.ReadyRaidProfileCount))
        {
            ClosePreloadedRaidAdmission();
            return;
        }

        if (ExpectedAdmissionDeadline.Expired(Runner))
        {
            Phase = MatchPhase.Starting;
            Runner.SessionInfo.IsOpen = false;
            Runner.SessionInfo.IsVisible = false;
            spawnManager?.AbortRaidingParticipantsForClosure();
            BeginClosure(RaidClosureReason.BootstrapFailure);
        }
    }

    private void BeginClosure(RaidClosureReason reason)
    {
        bool validPhase = Phase == MatchPhase.InProgress ||
                          (Phase == MatchPhase.Starting && reason == RaidClosureReason.BootstrapFailure);
        if (!validPhase || !HasStateAuthority)
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
        // Frozen-manifest/development admission reaches the same authoritative
        // Starting boundary as coded admission; it must not bypass the deferred
        // initial PvPvE bootstrap by assigning InProgress directly.
        if (!TryStartRaid())
        {
            Debug.LogError("[NetworkMatchController] Preloaded raid admission could not start the raid.", this);
        }
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
    HostCancellation = 1,
    BootstrapFailure = 2
}
