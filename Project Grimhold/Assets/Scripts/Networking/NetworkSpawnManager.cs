using Fusion;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Spawning;

/// <summary>
/// Server-authoritative manager responsible for admitting players and spawning characters/entities.
/// Lives on the persistent runner GameObject and maintains its lifecycle strictly aligned with the associated runner.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkSpawnManager : NetworkRunnerCallbacksAdapter
{
    internal enum HostMigrationRecoveryWindowState
    {
        Inactive,
        Open,
        Sealing,
        Closed,
        Failed
    }

    internal enum HostMigrationCompletionStatus
    {
        Success,
        Failure,
        Timeout
    }

    internal readonly struct HostMigrationCompletionResult
    {
        public HostMigrationCompletionStatus Status { get; }
        public string Details { get; }

        public bool Succeeded => Status == HostMigrationCompletionStatus.Success;

        public HostMigrationCompletionResult(
            HostMigrationCompletionStatus status,
            string details)
        {
            Status = status;
            Details = details ?? string.Empty;
        }
    }

    private enum SceneLoadProcessingState
    {
        None,
        Pending,
        Processing,
        Completed,
        Failed,
        AwaitingHostMigrationRestore,
        SnapshotRestoredAwaitingRuntimeRebind,
        HostMigrationRestoreFailed
    }

    public enum SceneSpawnConfigurationStatus
    {
        None,
        SpawnPointsNotRequired,
        SpawnPointsReady,
        Invalid
    }

    private enum InitialRaidBootstrapState
    {
        NotStarted,
        Running,
        Completed,
        Failed
    }

    private PlayerClassCatalog _playerClassCatalog;
    private NetworkPrefabRef _raidParticipantPrefab;
    private NetworkPrefabRef[] _enemyPrefabs;

    [SerializeField]
    private NetworkPrefabRef _lootContainerPrefab;

    [SerializeField]
    private NetworkPrefabRef _breakablePrefab;

    [SerializeField]
    private LootDefinitionCatalog _lootCatalog;

    private readonly HashSet<PlayerRef> _admittedPlayers = new();
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedAvatars = new();
    private readonly Dictionary<PlayerRef, RaidAdmissionData> _admissionData = new();
    private readonly ControlledReturnRegistry _controlledReturns = new();
    private readonly Dictionary<string, PlayerRef> _admittedProfiles = new();
    private readonly Dictionary<ProfileId, PlayerRef> _earlyHostMigrationReconnects = new();
    private readonly Dictionary<ProfileId, NetworkObject> _restoredHostMigrationParticipants = new();
    private readonly HashSet<ProfileId> _hostMigrationEligibleProfiles = new();
    private readonly HashSet<ProfileId> _hostMigrationUnresolvedProfiles = new();
    private readonly Dictionary<ProfileId, PlayerRef> _hostMigrationRecoveredProfiles = new();
    private readonly List<NetworkObject> _spawnedEnemies = new();
    private readonly List<NetworkObject> _cleanupBuffer = new();
    private readonly InitialLootSpawnState _lootSpawnState = new();
    private readonly InitialBreakableSpawnState _breakableSpawnState = new();

    private ulong _lootSessionSeed;
    private bool _hasLootSessionSeed;

    private readonly Dictionary<SpawnGroupType, Transform[]> _spawnPointLookup = new();

    private NetworkRunner _runner;
    private NetworkMatchController _matchController;
    private NetworkSpawnSceneConfiguration _sceneSpawnPointConfiguration;
    private SessionStartupContext _startupContext;
    private RaidLaunchContext _launchContext;

    private int _currentSceneLoadGeneration = 0;
    private int _lastCompletedSceneLoadGeneration = -1;
    private SceneLoadProcessingState _sceneLoadState = SceneLoadProcessingState.None;
    private SceneSpawnConfigurationStatus _sceneSpawnStatus = SceneSpawnConfigurationStatus.None;
    private bool _spawnsBlocked = true;
    private InitialRaidBootstrapState _initialRaidBootstrapState = InitialRaidBootstrapState.NotStarted;

    private bool _resumedScenePipelineReady;
    private bool _snapshotRestoreReported;
    private bool _snapshotRestoreSucceeded;
    private IReadOnlyDictionary<ProfileId, NetworkObject> _restoredParticipantsAwaitingRebind;
    private TaskCompletionSource<HostMigrationCompletionResult> _hostMigrationCompletion;
    private TaskCompletionSource<bool> _hostMigrationRosterChanged;
    private HostMigrationRecoveryWindowState _hostMigrationRecoveryState;
    private bool _hostMigrationSnapshotHadRaidingParticipant;

    internal HostMigrationRecoveryWindowState HostMigrationRecoveryState =>
        _hostMigrationRecoveryState;
    internal bool IsHostMigrationRecoveryInProgress =>
        _hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Open ||
        _hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Sealing;

    /// <summary>
    /// Exposes the linked coordinator.
    /// </summary>
    public NetworkMatchController MatchController => _matchController;
    public bool HasAdmittedRaidParticipants => _admittedProfiles.Count > 0;

    /// <summary>Returns whether an admitted participant is still actively raiding.</summary>
    public bool HasRaidingParticipants
    {
        get
        {
            foreach (NetworkObject participantObject in _spawnedPlayers.Values)
            {
                if (participantObject != null &&
                    participantObject.TryGetBehaviour(out NetworkRaidParticipant participant) &&
                    participant.State == RaidParticipantState.Raiding)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Returns whether an extracted participant still awaits TASK-80 confirmation.</summary>
    public bool HasPendingExtractionCommits
    {
        get
        {
            foreach (NetworkObject participantObject in _spawnedPlayers.Values)
            {
                if (participantObject != null &&
                    participantObject.TryGetBehaviour(out NetworkRaidParticipant participant) &&
                    participant.State == RaidParticipantState.Extracted &&
                    !participant.IsExtractionCommitConfirmed)
                {
                    return true;
                }
            }

            return false;
        }
    }
    public NetworkPrefabRef LootContainerPrefab => _lootContainerPrefab;
    public NetworkPrefabRef BreakablePrefab => _breakablePrefab;
    
    public bool ShouldInitializeMatchPhase => _startupContext.IsValid && _startupContext.ShouldInitializeMatchPhase;
    public bool IsScenePrepared => _sceneLoadState == SceneLoadProcessingState.Completed &&
                                   _sceneSpawnStatus != SceneSpawnConfigurationStatus.Invalid;
    public bool HasCompletedInitialRaidBootstrap => _initialRaidBootstrapState == InitialRaidBootstrapState.Completed;

    /// <summary>
    /// Resolves a connected RPC source to the exact participant PlayerObject and its
    /// operational Host role. After migration the operational Host may differ from the
    /// historical Host stored in the frozen launch context.
    /// </summary>
    internal bool TryResolveReturnRequester(
        NetworkRaidParticipant participant,
        PlayerRef source,
        out bool isOperationalHost,
        out string rejectionReason)
    {
        isOperationalHost = false;
        rejectionReason = null;
        if (_runner == null || !_runner.IsServer || participant == null || source.IsNone ||
            participant.Runner != _runner || participant.Object == null)
        {
            rejectionReason = "Runner, authority, participant, or RPC source is invalid.";
            return false;
        }

        NetworkObject requesterObject = _runner.GetPlayerObject(source);
        if (requesterObject == null || requesterObject != participant.Object ||
            !requesterObject.TryGetBehaviour(out NetworkRaidParticipant requester) || requester != participant)
        {
            rejectionReason = "RPC source does not resolve to the receiving participant PlayerObject.";
            return false;
        }

        string profileId = participant.ProfileId.ToString();
        string generationId = participant.RaidGenerationId.ToString();
        if (_launchContext == null || !_launchContext.HostProfileId.IsValid ||
            string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(generationId) ||
            _matchController == null ||
            !string.Equals(
                generationId,
                _matchController.RaidGenerationId.ToString(),
                StringComparison.Ordinal))
        {
            rejectionReason = "Canonical raid identity is unavailable or inconsistent.";
            return false;
        }

        isOperationalHost = source == _runner.LocalPlayer || string.Equals(
            profileId,
            _launchContext.HostProfileId.Value,
            StringComparison.Ordinal);
        return true;
    }

    /// <summary>
    /// Registers the one-shot departure evidence required before authorizing a defeated Client return.
    /// </summary>
    internal bool TryRegisterControlledReturn(
        NetworkRaidParticipant participant,
        out string rejectionReason)
    {
        rejectionReason = null;
        if (participant == null || participant.Runner != _runner ||
            participant.State != RaidParticipantState.Defeated || _matchController == null ||
            (_matchController.Phase != NetworkMatchController.MatchPhase.InProgress &&
             _matchController.Phase != NetworkMatchController.MatchPhase.Closing))
        {
            rejectionReason = "Participant is not a defeated member of an active raid.";
            return false;
        }

        var key = new ControlledReturnKey(
            participant.ProfileId.ToString(),
            participant.RaidGenerationId.ToString());
        if (!_controlledReturns.TryRegister(in key))
        {
            rejectionReason = "Controlled Return identity is invalid or already pending.";
            return false;
        }

        return true;
    }

    private void Awake()
    {
        // No global static instance registration or DontDestroyOnLoad here.
        // The Launcher adds this component to the persistent runner GameObject, which handles DontDestroyOnLoad.
    }

    private void OnDestroy()
    {
        _admittedPlayers.Clear();
        _spawnedPlayers.Clear();
        _spawnedAvatars.Clear();
        _admissionData.Clear();
        _controlledReturns.Clear();
        _admittedProfiles.Clear();
        _spawnedEnemies.Clear();
        _lootSpawnState.Clear();
        _breakableSpawnState.Clear();
        _lootSessionSeed = 0;
        _hasLootSessionSeed = false;
        _spawnPointLookup.Clear();
        _matchController = null;
        _runner = null;
        _sceneSpawnPointConfiguration = null;
        _startupContext = default;
        _launchContext = null;
        ClearHostMigrationRoster();
        _cleanupBuffer.Clear();
    }

    /// <summary>
    /// Explicitly binds the manager with a single active NetworkRunner instance.
    /// This must be called before starting the session and registering callbacks.
    /// </summary>
    public bool InitializeForRunner(
        NetworkRunner runner,
        PlayerClassCatalog catalog,
        NetworkPrefabRef raidParticipantPrefab,
        NetworkPrefabRef[] enemyPrefab,
        SessionStartupContext startupContext,
        RaidLaunchContext launchContext)
    {
        if (runner == null)
        {
            Debug.LogError("[NetworkSpawnManager] InitializeForRunner: runner is null.");
            return false;
        }

        if (!startupContext.IsValid)
        {
            Debug.LogError("[NetworkSpawnManager] InitializeForRunner: Invalid startup context.");
            return false;
        }

        // Return true if already initialized for the same active runner (idempotent)
        if (_runner == runner)
        {
            return true;
        }

        // Reject if trying to associate with a different runner when previous is active
        if (_runner != null && _runner.IsRunning)
        {
            Debug.LogError("[NetworkSpawnManager] InitializeForRunner: Manager is already associated with another active runner.");
            return false;
        }

        _runner = runner;
        _playerClassCatalog = catalog;
        _raidParticipantPrefab = raidParticipantPrefab;
        _enemyPrefabs = enemyPrefab;
        _startupContext = startupContext;
        _launchContext = launchContext;

        _admittedPlayers.Clear();
        _spawnedPlayers.Clear();
        _spawnedAvatars.Clear();
        _admissionData.Clear();
        _controlledReturns.Clear();
        _admittedProfiles.Clear();
        ClearHostMigrationRoster();
        _spawnedEnemies.Clear();
        _lootSpawnState.Clear();
        _breakableSpawnState.Clear();
        _lootSessionSeed = 0;
        _hasLootSessionSeed = false;
        _spawnPointLookup.Clear();
        _matchController = null;
        _sceneSpawnPointConfiguration = null;

        _currentSceneLoadGeneration = 0;
        _lastCompletedSceneLoadGeneration = -1;
        _sceneLoadState = SceneLoadProcessingState.None;
        _sceneSpawnStatus = SceneSpawnConfigurationStatus.None;
        _spawnsBlocked = true;
        _initialRaidBootstrapState = InitialRaidBootstrapState.NotStarted;

        _resumedScenePipelineReady = false;
        _snapshotRestoreReported = false;
        _snapshotRestoreSucceeded = false;
        _restoredParticipantsAwaitingRebind = null;
        _hostMigrationCompletion = startupContext.Mode == SessionStartupMode.HostMigrationResume
            ? new TaskCompletionSource<HostMigrationCompletionResult>(
                TaskCreationOptions.RunContinuationsAsynchronously)
            : null;
        _hostMigrationRosterChanged = startupContext.Mode == SessionStartupMode.HostMigrationResume
            ? CreateRosterSignal()
            : null;
        _hostMigrationRecoveryState = HostMigrationRecoveryWindowState.Inactive;
        _hostMigrationSnapshotHadRaidingParticipant = false;

        Debug.Log($"[NetworkSpawnManager] Initialized for runner: {runner.name}");
        return true;
    }

    /// <summary>
    /// Merges scene-owned prefab references into this runner-scoped persistent manager.
    /// Existing launcher-owned player and enemy references remain unchanged.
    /// </summary>
    public bool CopyReferencesFrom(NetworkSpawnManager configuredManager)
    {
        if (configuredManager == null || ReferenceEquals(configuredManager, this))
        {
            return false;
        }

        _lootContainerPrefab = configuredManager._lootContainerPrefab;
        _breakablePrefab = configuredManager._breakablePrefab;
        _lootCatalog = configuredManager._lootCatalog;
        if (!_lootContainerPrefab.IsValid)
        {
            Debug.LogError(
                $"[NetworkSpawnManager] Scene manager '{configuredManager.name}' has no valid loot-container prefab. Loot groups will be skipped.",
                configuredManager);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Explicitly binds the match coordinator to this manager.
    /// </summary>
    public bool BindMatchController(NetworkMatchController coordinator)
    {
        if (_runner == null)
        {
            Debug.LogError("[NetworkSpawnManager] BindMatchController: Manager has not been initialized for a runner.");
            return false;
        }

        if (coordinator == null)
        {
            Debug.LogError("[NetworkSpawnManager] BindMatchController: Match coordinator is null.");
            return false;
        }

        if (coordinator.Runner != _runner)
        {
            Debug.LogError("[NetworkSpawnManager] BindMatchController: Coordinator belongs to a different runner.");
            return false;
        }

        if (_matchController != null && _matchController != coordinator)
        {
            Debug.LogError("[NetworkSpawnManager] BindMatchController: Another coordinator is already bound to this manager.");
            return false;
        }

        _matchController = coordinator;
        Debug.Log($"[NetworkSpawnManager] Coordinator bound successfully to runner {_runner.name}");
        return true;
    }

    /// <summary>
    /// Configures spawn points and groups using the scene's spatial configuration.
    /// </summary>
    public void ConfigureForScene(NetworkSpawnSceneConfiguration config)
    {
        if (config == null) return;

        if (!config.Validate(out string error))
        {
            Debug.LogError($"[NetworkSpawnManager] Scene configuration validation failed: {error}");
            return;
        }

        // Validate that all spawn points belong strictly to the config's scene
        if (config.SpawnGroups != null)
        {
            foreach (var definition in config.SpawnGroups)
            {
                if (definition != null && definition.SpawnPoints != null)
                {
                    foreach (var sp in definition.SpawnPoints)
                    {
                        if (sp != null && sp.gameObject.scene != config.gameObject.scene)
                        {
                            Debug.LogError($"[NetworkSpawnManager] Spawn point '{sp.name}' does not belong to scene '{config.gameObject.scene.name}'. Spawning aborted.");
                            return;
                        }
                    }
                }
            }
        }

        _sceneSpawnPointConfiguration = config;
        _spawnPointLookup.Clear();

        if (config.SpawnGroups != null)
        {
            foreach (var definition in config.SpawnGroups)
            {
                if (definition != null && definition.SpawnPoints != null)
                {
                    if (!_spawnPointLookup.ContainsKey(definition.Group))
                    {
                        _spawnPointLookup.Add(definition.Group, definition.SpawnPoints);
                    }
                }
            }
        }
        Debug.Log("[NetworkSpawnManager] Scene configuration applied successfully.");
    }

    public override void OnSceneLoadStart(NetworkRunner runner)
    {
        if (runner != _runner)
            return;

        // Invalidate previous scene configs and transforms immediately
        _spawnPointLookup.Clear();
        _sceneSpawnPointConfiguration = null;
        _lootSpawnState.Clear();
        _breakableSpawnState.Clear();

        // Increment scene load generation to build a unique load identity
        _currentSceneLoadGeneration++;
        _sceneLoadState = SceneLoadProcessingState.Pending;
        _sceneSpawnStatus = SceneSpawnConfigurationStatus.None;
        _spawnsBlocked = true;
        _initialRaidBootstrapState = InitialRaidBootstrapState.NotStarted;

        Debug.Log($"[NetworkSpawnManager] OnSceneLoadStart: Starting load generation {_currentSceneLoadGeneration} (State: {_sceneLoadState}). Blocked spawning and cleared spatial config.");
    }

    public override void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner != _runner)
            return;

        if (_startupContext.IsValid &&
            _startupContext.Mode == SessionStartupMode.HostMigrationResume)
        {
            SignalRosterChanged();
        }

        if (!runner.IsServer)
            return;

        int thisLoadIdentity = _currentSceneLoadGeneration;

        // Reject if not pending or already completed
        if (_sceneLoadState != SceneLoadProcessingState.Pending)
        {
            Debug.LogWarning($"[NetworkSpawnManager] OnSceneLoadDone: Load state is {_sceneLoadState} (expected Pending). Skipping duplicate spawn processing.");
            return;
        }

        if (thisLoadIdentity == _lastCompletedSceneLoadGeneration)
        {
            Debug.LogWarning($"[NetworkSpawnManager] OnSceneLoadDone: Generation {thisLoadIdentity} is already marked completed. Skipping.");
            return;
        }

        // Change state to Processing immediately to prevent concurrent callback execution
        _sceneLoadState = SceneLoadProcessingState.Processing;

        // Ensure runner has a valid SceneManager
        if (runner.SceneManager == null)
        {
            Debug.LogError("[NetworkSpawnManager] OnSceneLoadDone: runner.SceneManager is null. Spawning aborted.");
            FailSceneLoadPipeline();
            return;
        }

        // Resolve runnerScene
        Scene runnerScene = runner.SceneManager.MainRunnerScene;
        if (!runnerScene.IsValid() || !runnerScene.isLoaded || !runner.SceneManager.IsRunnerScene(runnerScene))
        {
            Debug.LogError($"[NetworkSpawnManager] OnSceneLoadDone: MainRunnerScene is invalid, not loaded, or not a runner scene. Spawning aborted.");
            FailSceneLoadPipeline();
            return;
        }

        // Find configurations strictly within root objects of the runnerScene and their children
        NetworkSpawnSceneConfiguration sceneConfig = null;
        NetworkSpawnManager configuredSceneManager = null;
        int configCount = 0;
        int configuredManagerCount = 0;
        try
        {
            GameObject[] rootObjects = runnerScene.GetRootGameObjects();
            foreach (var go in rootObjects)
            {
                if (go == null) continue;
                var configsInRoot = go.GetComponentsInChildren<NetworkSpawnSceneConfiguration>(true);
                foreach (var c in configsInRoot)
                {
                    if (c != null)
                    {
                        sceneConfig = c;
                        configCount++;
                    }
                }

                var managersInRoot = go.GetComponentsInChildren<NetworkSpawnManager>(true);
                foreach (NetworkSpawnManager manager in managersInRoot)
                {
                    if (manager != null && !ReferenceEquals(manager, this) && manager.gameObject.scene == runnerScene)
                    {
                        configuredSceneManager = manager;
                        configuredManagerCount++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkSpawnManager] Exception resolving scene configurations: {ex.Message}. Spawning aborted.");
            FailSceneLoadPipeline();
            return;
        }

        // Apply explicit scene spawning policy
        if (configCount == 0)
        {
            // Cero configuraciones: no asumimos éxito. Tratamos como inválida/falla (política no declarada).
            Debug.LogError($"[NetworkSpawnManager] Scene '{runnerScene.name}' does not contain any NetworkSpawnSceneConfiguration. Spawning aborted.");
            FailSceneLoadPipeline();
            return;
        }
        else if (configCount > 1)
        {
            Debug.LogError($"[NetworkSpawnManager] Multiple NetworkSpawnSceneConfiguration components found in scene '{runnerScene.name}'! Spawning aborted.");
            FailSceneLoadPipeline();
            return;
        }

        // Validate and apply configuration
        if (sceneConfig.gameObject.scene != runnerScene)
        {
            Debug.LogError($"[NetworkSpawnManager] Config '{sceneConfig.name}' belongs to a different scene. Spawning aborted.");
            FailSceneLoadPipeline();
            return;
        }

        if (configuredManagerCount > 1)
        {
            Debug.LogError($"[NetworkSpawnManager] Multiple scene-configured NetworkSpawnManager components found in '{runnerScene.name}'. Spawning aborted.");
            FailSceneLoadPipeline();
            return;
        }

        if (configuredSceneManager != null)
        {
            CopyReferencesFrom(configuredSceneManager);
            Destroy(configuredSceneManager);
        }

        if (!sceneConfig.Validate(out string validationError))
        {
            Debug.LogError($"[NetworkSpawnManager] Scene configuration validation failed: {validationError}. Spawning aborted.");
            FailSceneLoadPipeline();
            return;
        }

        // Determine spawn configuration status
        if (sceneConfig.SpawnPointPolicy == SceneSpawnPointPolicy.NotRequired)
        {
            _sceneSpawnStatus = SceneSpawnConfigurationStatus.SpawnPointsNotRequired;
            _spawnPointLookup.Clear();
            _sceneSpawnPointConfiguration = null;
            Debug.Log($"[NetworkSpawnManager] Scene '{runnerScene.name}' loaded. Scene does not require configured spawn points.");
        }
        else
        {
            _sceneSpawnStatus = SceneSpawnConfigurationStatus.SpawnPointsReady;
            ConfigureForScene(sceneConfig);
            if (!TryValidatePlayerSpawnPreflight(out string playerSpawnFailure))
            {
                Debug.LogError($"[NetworkSpawnManager] Player spawn preflight failed: {playerSpawnFailure}");
                FailSceneLoadPipeline();
                return;
            }
        }

        // Start processing spawns
        try
        {
            // Scene loading prepares spatial state and admits already-registered players.
            // Initial PvPvE content is deliberately deferred until Start Raid.
            if (_sceneSpawnStatus == SceneSpawnConfigurationStatus.SpawnPointsReady)
            {
                foreach (PlayerRef player in runner.ActivePlayers)
                {
                    if (_admittedPlayers.Contains(player))
                    {
                        SpawnPlayer(runner, player);
                    }
                }
            }

            if (_startupContext.ShouldExecuteInitialSceneBootstrap)
            {
                // Mark scene preparation complete. Initial PvPvE content is not part of this callback.
                _lastCompletedSceneLoadGeneration = thisLoadIdentity;
                _sceneLoadState = SceneLoadProcessingState.Completed;
                _spawnsBlocked = false;
                Debug.Log($"[NetworkSpawnManager] OnSceneLoadDone: Load generation {thisLoadIdentity} prepared successfully (Status: {_sceneSpawnStatus}). Participant spawns unblocked; initial PvPvE bootstrap is pending Start Raid.");
            }
            else
            {
                _resumedScenePipelineReady = true;
                TryAdvanceHostMigrationRestoreState();
                Debug.Log($"[NetworkSpawnManager] OnSceneLoadDone: Scene load pipeline ready. Awaiting Host Migration Restore. Spawns remain blocked.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkSpawnManager] Exception during spawn processing: {ex.Message}");
            FailSceneLoadPipeline();
        }
    }

    /// <summary>
    /// Executes the one-time initial PvPvE content bootstrap after the authoritative
    /// match has entered Starting. Scene preparation and participant spawning are
    /// intentionally completed before this method is called.
    /// </summary>
    public bool TryExecuteInitialRaidBootstrap(out string failure)
    {
        failure = null;
        if (_runner == null || !_runner.IsServer)
        {
            failure = "Initial raid bootstrap requires the active server runner.";
            return false;
        }

        if (_startupContext.Mode != SessionStartupMode.FreshSession)
        {
            failure = "Host Migration Resume cannot execute a fresh raid bootstrap.";
            return false;
        }

        if (_matchController == null || _matchController.Phase != NetworkMatchController.MatchPhase.Starting)
        {
            failure = "Initial raid bootstrap requires the match to be in Starting.";
            return false;
        }

        if (!IsScenePrepared)
        {
            failure = "Initial raid bootstrap requires a prepared Gameplay scene.";
            return false;
        }

        if (_initialRaidBootstrapState != InitialRaidBootstrapState.NotStarted)
        {
            failure = $"Initial raid bootstrap is already {_initialRaidBootstrapState}.";
            return false;
        }

        _initialRaidBootstrapState = InitialRaidBootstrapState.Running;
        try
        {
            if (_sceneSpawnPointConfiguration?.SpawnGroups == null)
            {
                _initialRaidBootstrapState = InitialRaidBootstrapState.Completed;
                return true;
            }

            foreach (SpawnGroupDefinition group in _sceneSpawnPointConfiguration.SpawnGroups)
            {
                if (group == null)
                {
                    continue;
                }

                switch (InitialSpawnGroupPolicy.Resolve(group.Group))
                {
                    case InitialSpawnGroupPolicy.SpawnKind.Players:
                        break;
                    case InitialSpawnGroupPolicy.SpawnKind.Enemies:
                        for (int index = 0; index < group.Amount; index++)
                        {
                            if (!SpawnEnemy(_runner))
                            {
                                failure = $"Enemy bootstrap failed for group '{group.Group}'.";
                                _initialRaidBootstrapState = InitialRaidBootstrapState.Failed;
                                return false;
                            }
                        }
                        break;
                    case InitialSpawnGroupPolicy.SpawnKind.LootContainers:
                        if (!SpawnConfiguredLootContainers(_runner, group))
                        {
                            failure = $"Loot bootstrap failed for group '{group.Group}'.";
                            _initialRaidBootstrapState = InitialRaidBootstrapState.Failed;
                            return false;
                        }
                        break;
                    case InitialSpawnGroupPolicy.SpawnKind.Breakables:
                        if (!SpawnConfiguredBreakables(_runner, group))
                        {
                            failure = $"Breakables bootstrap failed for group '{group.Group}'.";
                            _initialRaidBootstrapState = InitialRaidBootstrapState.Failed;
                            return false;
                        }
                        break;
                    default:
                        Debug.LogWarning(
                            $"[NetworkSpawnManager] Initial spawning for group '{group.Group}' is not implemented. The group was skipped.",
                            this);
                        break;
                }
            }

            _initialRaidBootstrapState = InitialRaidBootstrapState.Completed;
            Debug.Log("[NetworkSpawnManager] Initial PvPvE bootstrap completed successfully.", this);
            return true;
        }
        catch (Exception exception)
        {
            _initialRaidBootstrapState = InitialRaidBootstrapState.Failed;
            failure = exception.Message;
            Debug.LogException(exception, this);
            return false;
        }
    }

    private void FailSceneLoadPipeline()
    {
        _sceneSpawnStatus = SceneSpawnConfigurationStatus.Invalid;
        _sceneLoadState = SceneLoadProcessingState.Failed;
        _spawnsBlocked = true;
        _spawnPointLookup.Clear();
        _sceneSpawnPointConfiguration = null;
        CompleteHostMigration(
            HostMigrationCompletionStatus.Failure,
            "The resumed scene load pipeline failed.");
    }

    /// <summary>
    /// Waits for this replacement runner's snapshot restore and runtime rebind pipeline
    /// to reach a one-shot terminal state.
    /// </summary>
    internal async Task<HostMigrationCompletionResult> WaitForHostMigrationCompletionAsync(
        TimeSpan timeout)
    {
        return await WaitForHostMigrationCompletionAsync(timeout, CancellationToken.None);
    }

    internal async Task<HostMigrationCompletionResult> WaitForHostMigrationCompletionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource<HostMigrationCompletionResult> completion =
            _hostMigrationCompletion;
        if (completion == null)
        {
            return new HostMigrationCompletionResult(
                HostMigrationCompletionStatus.Failure,
                "This spawn manager was not initialized for Host Migration Resume.");
        }

        if (completion.Task.IsCompleted)
        {
            return await completion.Task;
        }

        if (timeout <= TimeSpan.Zero)
        {
            completion.TrySetResult(CreateHostMigrationTimeoutResult());
            return await completion.Task;
        }

        long lifecycleDeadline = System.Diagnostics.Stopwatch.GetTimestamp() +
            (long)(timeout.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);
        while (!completion.Task.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Open)
            {
                TimeSpan remaining = GetRemaining(lifecycleDeadline);
                TimeSpan recoveryWindow = remaining < TimeSpan.FromSeconds(30)
                    ? remaining
                    : TimeSpan.FromSeconds(30);
                await RunHostMigrationRecoveryWindowAsync(
                    recoveryWindow,
                    lifecycleDeadline,
                    cancellationToken);
                continue;
            }

            TimeSpan wait = GetRemaining(lifecycleDeadline);
            if (wait <= TimeSpan.Zero)
            {
                completion.TrySetResult(CreateHostMigrationTimeoutResult());
                break;
            }

            Task rosterChanged = GetRosterChangedTask();
            Task delay = Task.Delay(wait, cancellationToken);
            await Task.WhenAny(completion.Task, rosterChanged, delay);
        }

        return await completion.Task;
    }

    internal async Task WaitForHostMigrationRecoveryWindowOpenAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (_hostMigrationCompletion == null || _runner == null || !_runner.IsServer)
        {
            throw new InvalidOperationException(
                "Only a replacement server can await the recovery window.");
        }

        long deadline = System.Diagnostics.Stopwatch.GetTimestamp() +
            (long)(timeout.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);
        while (_hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Inactive)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan remaining = GetRemaining(deadline);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(
                    "Snapshot restore did not open the recovery window within the restore budget.");
            }

            Task changed = GetRosterChangedTask();
            Task delay = Task.Delay(remaining, cancellationToken);
            await Task.WhenAny(_hostMigrationCompletion.Task, changed, delay);
        }

        if (_hostMigrationRecoveryState != HostMigrationRecoveryWindowState.Open &&
            _hostMigrationRecoveryState != HostMigrationRecoveryWindowState.Closed)
        {
            HostMigrationCompletionResult completion =
                _hostMigrationCompletion.Task.IsCompleted
                    ? await _hostMigrationCompletion.Task
                    : default;
            throw new InvalidOperationException(
                $"Snapshot restore could not open recovery. State={_hostMigrationRecoveryState}. " +
                completion.Details);
        }
    }

    public void ReportSnapshotRestoreResult(
        bool success,
        IReadOnlyDictionary<ProfileId, NetworkObject> restoredParticipants = null)
    {
        if (_snapshotRestoreReported)
        {
            Debug.LogWarning("[NetworkSpawnManager] Snapshot restore result already reported. Ignoring duplicate.");
            return;
        }

        _snapshotRestoreReported = true;
        _snapshotRestoreSucceeded = success;
        _restoredParticipantsAwaitingRebind = restoredParticipants;
        Debug.Log(
            $"[HM-MULTI] Snapshot restore reported. Success={success}.",
            this);
        TryAdvanceHostMigrationRestoreState(restoredParticipants);
    }

    private void TryAdvanceHostMigrationRestoreState(
        IReadOnlyDictionary<ProfileId, NetworkObject> restoredParticipants = null)
    {
        if (_sceneLoadState == SceneLoadProcessingState.Failed || _sceneLoadState == SceneLoadProcessingState.HostMigrationRestoreFailed)
            return;
        if (_hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Failed)
            return;

        restoredParticipants ??= _restoredParticipantsAwaitingRebind;

        if (_snapshotRestoreReported && !_snapshotRestoreSucceeded)
        {
            _sceneLoadState = SceneLoadProcessingState.HostMigrationRestoreFailed;
            _spawnsBlocked = true;
            Debug.LogError("[NetworkSpawnManager] Host Migration Restore failed.");
            CompleteHostMigration(
                HostMigrationCompletionStatus.Failure,
                "Snapshot restoration reported failure.");
            _hostMigrationRecoveryState = HostMigrationRecoveryWindowState.Failed;
            SignalRosterChanged();
            return;
        }

        if (!_resumedScenePipelineReady || !_snapshotRestoreReported)
        {
            if (_sceneLoadState != SceneLoadProcessingState.Pending &&
                _sceneLoadState != SceneLoadProcessingState.Processing)
            {
                _sceneLoadState = SceneLoadProcessingState.AwaitingHostMigrationRestore;
            }
            _spawnsBlocked = true;
            return;
        }

        if (_snapshotRestoreSucceeded)
        {
            try
            {
                _sceneLoadState = SceneLoadProcessingState.SnapshotRestoredAwaitingRuntimeRebind;
                InitializeHostMigrationRoster(restoredParticipants);
                _hostMigrationRecoveryState = HostMigrationRecoveryWindowState.Open;
                DrainEarlyHostMigrationReconnects();
                SignalRosterChanged();
                Debug.Log(
                    $"[HM-MULTI] Recovery window opened. " +
                    $"Eligible={_hostMigrationEligibleProfiles.Count}, " +
                    $"Recovered={_hostMigrationRecoveredProfiles.Count}.",
                    this);
            }
            catch (Exception exception)
            {
                _sceneLoadState = SceneLoadProcessingState.HostMigrationRestoreFailed;
                _spawnsBlocked = true;
                Debug.LogException(exception, this);
                CompleteHostMigration(
                    HostMigrationCompletionStatus.Failure,
                    $"Runtime rebind failed: {exception.Message}");
                _hostMigrationRecoveryState = HostMigrationRecoveryWindowState.Failed;
                SignalRosterChanged();
            }
        }
        else
        {
            _sceneLoadState = SceneLoadProcessingState.HostMigrationRestoreFailed;
            _spawnsBlocked = true;
            Debug.LogError("[NetworkSpawnManager] Host Migration Restore failed.");
            CompleteHostMigration(
                HostMigrationCompletionStatus.Failure,
                "Snapshot restoration reported failure.");
            _hostMigrationRecoveryState = HostMigrationRecoveryWindowState.Failed;
            SignalRosterChanged();
        }
    }

    private void ClearPendingHostMigrationRebindData()
    {
        _restoredParticipantsAwaitingRebind = null;
    }

    private void ClearHostMigrationRoster()
    {
        _earlyHostMigrationReconnects.Clear();
        _restoredHostMigrationParticipants.Clear();
        _hostMigrationEligibleProfiles.Clear();
        _hostMigrationUnresolvedProfiles.Clear();
        _hostMigrationRecoveredProfiles.Clear();
        _hostMigrationSnapshotHadRaidingParticipant = false;
        _hostMigrationRecoveryState = HostMigrationRecoveryWindowState.Inactive;
        _hostMigrationRosterChanged = CreateRosterSignal();
        ClearPendingHostMigrationRebindData();
    }

    private async Task RunHostMigrationRecoveryWindowAsync(
        TimeSpan recoveryWindow,
        long lifecycleDeadline,
        CancellationToken cancellationToken)
    {
        long recoveryDeadline = System.Diagnostics.Stopwatch.GetTimestamp() +
            (long)(recoveryWindow.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);

        while (_hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Open)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (AreAllEligibleProfilesRecovered())
            {
                SealHostMigrationRoster();
                return;
            }

            long effectiveDeadline = Math.Min(recoveryDeadline, lifecycleDeadline);
            TimeSpan remaining = GetRemaining(effectiveDeadline);
            if (remaining <= TimeSpan.Zero)
            {
                SealHostMigrationRoster();
                return;
            }

            Task rosterChanged = GetRosterChangedTask();
            Task delay = Task.Delay(remaining, cancellationToken);
            await Task.WhenAny(rosterChanged, delay);
        }
    }

    private void InitializeHostMigrationRoster(
        IReadOnlyDictionary<ProfileId, NetworkObject> restoredParticipants)
    {
        if (_runner == null || !_runner.IsServer || _matchController == null ||
            _launchContext == null || restoredParticipants == null)
        {
            throw new InvalidOperationException(
                "Host Migration roster requires the replacement server, MatchController, launch context, and restored participants.");
        }

        string generationId = _matchController.RaidGenerationId.ToString();
        if (string.IsNullOrWhiteSpace(generationId))
        {
            throw new InvalidOperationException("Restored RaidGenerationId is invalid.");
        }

        _restoredHostMigrationParticipants.Clear();
        _hostMigrationEligibleProfiles.Clear();
        _hostMigrationUnresolvedProfiles.Clear();
        _hostMigrationRecoveredProfiles.Clear();
        _hostMigrationSnapshotHadRaidingParticipant = false;
        var restoredAvatarIds = new HashSet<NetworkId>();

        foreach (KeyValuePair<ProfileId, NetworkObject> pair in restoredParticipants)
        {
            ProfileId profileId = pair.Key;
            NetworkObject participantObject = pair.Value;
            if (!profileId.IsValid || participantObject == null ||
                !participantObject.TryGetBehaviour(out NetworkRaidParticipant participant) ||
                !string.Equals(
                    participant.RaidGenerationId.ToString(),
                    generationId,
                    StringComparison.Ordinal) ||
                !RaidSessionRules.ContainsProfile(_launchContext.ParticipantProfileIds, profileId) ||
                !_restoredHostMigrationParticipants.TryAdd(profileId, participantObject))
            {
                throw new InvalidOperationException(
                    $"Restored participant '{profileId}' is duplicated or inconsistent with the Raid generation.");
            }

            if (participant.State == RaidParticipantState.Raiding &&
                (!participant.TryResolveCurrentAvatar(out NetworkObject restoredAvatar) ||
                 restoredAvatar == null ||
                 !restoredAvatarIds.Add(restoredAvatar.Id)))
            {
                throw new InvalidOperationException(
                    $"Restored Raiding participant '{profileId}' has a missing or duplicated avatar.");
            }

            bool terminal = participant.IsReturnAuthorized ||
                participant.State == RaidParticipantState.Extracted ||
                participant.State == RaidParticipantState.Aborted;
            _hostMigrationSnapshotHadRaidingParticipant |=
                participant.State == RaidParticipantState.Raiding;

            if (profileId == _launchContext.HostProfileId)
            {
                _hostMigrationUnresolvedProfiles.Add(profileId);
                continue;
            }

            if (IsHostMigrationRecoveryEligible(
                    profileId,
                    _launchContext.HostProfileId,
                    participant.State,
                    participant.IsReturnAuthorized,
                    terminalKnown: terminal))
            {
                _hostMigrationEligibleProfiles.Add(profileId);
                _hostMigrationUnresolvedProfiles.Add(profileId);
            }
            else
            {
                _hostMigrationUnresolvedProfiles.Add(profileId);
                _controlledReturns.MarkTerminal(
                    new ControlledReturnKey(profileId.Value, generationId));
            }
        }
    }

    internal static bool IsHostMigrationRecoveryEligible(
        ProfileId profileId,
        ProfileId oldHostProfileId,
        RaidParticipantState state,
        bool isReturnAuthorized,
        bool terminalKnown)
    {
        return profileId.IsValid && profileId != oldHostProfileId &&
               !isReturnAuthorized && !terminalKnown &&
               (state == RaidParticipantState.Raiding ||
                state == RaidParticipantState.Defeated);
    }

    private bool AreAllEligibleProfilesRecovered()
    {
        foreach (ProfileId profileId in _hostMigrationEligibleProfiles)
        {
            if (!_hostMigrationRecoveredProfiles.ContainsKey(profileId))
            {
                return false;
            }
        }

        return true;
    }

    private void DrainEarlyHostMigrationReconnects()
    {
        if (_hostMigrationRecoveryState != HostMigrationRecoveryWindowState.Open)
        {
            return;
        }

        var arrivals = new List<KeyValuePair<ProfileId, PlayerRef>>(
            _earlyHostMigrationReconnects);
        for (int index = 0; index < arrivals.Count; index++)
        {
            KeyValuePair<ProfileId, PlayerRef> arrival = arrivals[index];
            if (!TryRebindHostMigrationProfile(arrival.Key, arrival.Value) &&
                !_hostMigrationEligibleProfiles.Contains(arrival.Key))
            {
                _earlyHostMigrationReconnects.Remove(arrival.Key);
                if (arrival.Value != _runner.LocalPlayer)
                {
                    _runner.Disconnect(arrival.Value);
                }
            }
        }
    }

    private bool TryRebindHostMigrationProfile(ProfileId profileId, PlayerRef player)
    {
        if (_hostMigrationRecoveryState != HostMigrationRecoveryWindowState.Open ||
            !_hostMigrationEligibleProfiles.Contains(profileId) ||
            !_hostMigrationUnresolvedProfiles.Contains(profileId) ||
            !_restoredHostMigrationParticipants.TryGetValue(
                profileId,
                out NetworkObject participantObject) ||
            participantObject == null ||
            !participantObject.TryGetBehaviour(out NetworkRaidParticipant participant))
        {
            return false;
        }

        foreach (KeyValuePair<ProfileId, PlayerRef> recovered in _hostMigrationRecoveredProfiles)
        {
            if (recovered.Value == player && recovered.Key != profileId)
            {
                return false;
            }
        }

        participantObject.AssignInputAuthority(player);
        _runner.SetPlayerObject(player, participantObject);
        _admittedPlayers.Add(player);
        _spawnedPlayers[player] = participantObject;
        _admittedProfiles[profileId.Value] = player;

        if (participant.State == RaidParticipantState.Raiding)
        {
            if (!participant.TryResolveCurrentAvatar(out NetworkObject avatar) || avatar == null)
            {
                throw new InvalidOperationException(
                    $"Raiding participant '{profileId}' has no restored avatar.");
            }

            avatar.AssignInputAuthority(player);
            _spawnedAvatars[player] = avatar;
        }

        _hostMigrationRecoveredProfiles[profileId] = player;
        _hostMigrationUnresolvedProfiles.Remove(profileId);
        _earlyHostMigrationReconnects.Remove(profileId);
        TryBindRecoveredLocalPresentation(profileId, player, participant);
        SignalRosterChanged();
        Debug.Log(
            $"[HM-MULTI] Participant rebound. ProfileId={profileId}, NewPlayerRef={player}, " +
            $"State={participant.State}.",
            this);
        return true;
    }

    private void SealHostMigrationRoster()
    {
        if (_hostMigrationRecoveryState != HostMigrationRecoveryWindowState.Open)
        {
            return;
        }

        _hostMigrationRecoveryState = HostMigrationRecoveryWindowState.Sealing;
        try
        {
            ReconcileRecoveredProfilesWithActivePlayers();
            FinalizeUnrecoveredHostMigrationProfiles();
            if (!ValidateSealedHostMigrationRoster(out string failure))
            {
                throw new InvalidOperationException(failure);
            }

            _earlyHostMigrationReconnects.Clear();
            _hostMigrationUnresolvedProfiles.Clear();
            _sceneLoadState = SceneLoadProcessingState.Completed;
            _spawnsBlocked = false;
            _runner.SessionInfo.IsOpen = false;
            _runner.SessionInfo.IsVisible = false;
            _matchController.RestoreHostMigrationParticipantObservation(
                _hostMigrationSnapshotHadRaidingParticipant);
            _hostMigrationRecoveryState = HostMigrationRecoveryWindowState.Closed;
            ClearPendingHostMigrationRebindData();
            CompleteHostMigration(
                HostMigrationCompletionStatus.Success,
                "Snapshot restored, recovery roster sealed, and runtime mappings rebound.");
            Debug.Log("[HM-MULTI] Recovery roster sealed successfully.", this);
        }
        catch (Exception exception)
        {
            _hostMigrationRecoveryState = HostMigrationRecoveryWindowState.Failed;
            _spawnsBlocked = true;
            Debug.LogException(exception, this);
            CompleteHostMigration(
                HostMigrationCompletionStatus.Failure,
                $"Recovery roster sealing failed: {exception.Message}");
        }
        finally
        {
            SignalRosterChanged();
        }
    }

    private void ReconcileRecoveredProfilesWithActivePlayers()
    {
        var activePlayers = new HashSet<PlayerRef>(_runner.ActivePlayers);
        var recovered = new List<KeyValuePair<ProfileId, PlayerRef>>(
            _hostMigrationRecoveredProfiles);
        for (int index = 0; index < recovered.Count; index++)
        {
            ProfileId profileId = recovered[index].Key;
            PlayerRef player = recovered[index].Value;
            NetworkObject expected = _restoredHostMigrationParticipants[profileId];
            if (!expected.TryGetBehaviour(out NetworkRaidParticipant participant) ||
                participant.IsReturnAuthorized ||
                (participant.State != RaidParticipantState.Raiding &&
                 participant.State != RaidParticipantState.Defeated))
            {
                RemoveRecoveredHostMigrationProfile(profileId, player, requeue: false);
                _hostMigrationEligibleProfiles.Remove(profileId);
                _hostMigrationUnresolvedProfiles.Add(profileId);
                continue;
            }

            bool playerObjectMatches =
                _runner.TryGetPlayerObject(player, out NetworkObject actual) &&
                actual == expected;
            if (!IsRecoveredMappingCurrent(
                    player,
                    activePlayers,
                    playerObjectMatches))
            {
                RemoveRecoveredHostMigrationProfile(profileId, player, requeue: true);
            }
        }
    }

    internal static bool IsRecoveredMappingCurrent(
        PlayerRef player,
        ISet<PlayerRef> activePlayers,
        bool playerObjectMatches)
    {
        return !player.IsNone && activePlayers != null &&
               activePlayers.Contains(player) && playerObjectMatches;
    }

    private void FinalizeUnrecoveredHostMigrationProfiles()
    {
        var unresolved = new List<ProfileId>(_hostMigrationUnresolvedProfiles);
        string generationId = _matchController.RaidGenerationId.ToString();
        for (int index = 0; index < unresolved.Count; index++)
        {
            ProfileId profileId = unresolved[index];
            if (!_restoredHostMigrationParticipants.TryGetValue(
                    profileId,
                    out NetworkObject participantObject) ||
                participantObject == null ||
                !participantObject.TryGetBehaviour(out NetworkRaidParticipant participant))
            {
                throw new InvalidOperationException(
                    $"Unrecovered participant '{profileId}' is missing during sealing.");
            }

            NetworkObject avatar = null;
            participant.TryResolveCurrentAvatar(out avatar);
            if (avatar == null && participant.State != RaidParticipantState.Defeated)
            {
                TryFindLinkedAvatarOrCorpse(participantObject.Id, out avatar);
            }

            NetworkObject defeatedCorpse = null;
            if (participant.State == RaidParticipantState.Defeated &&
                (!TryFindLinkedAvatarOrCorpse(participantObject.Id, out defeatedCorpse) ||
                 defeatedCorpse == null ||
                 !defeatedCorpse.HasStateAuthority ||
                 defeatedCorpse.InputAuthority != PlayerRef.None ||
                 defeatedCorpse.GetComponent<NetworkLootContainer>() == null))
            {
                throw new InvalidOperationException(
                    $"Unrecovered Defeated profile '{profileId}' has no authoritative lootable corpse.");
            }

            if (participant.State == RaidParticipantState.Raiding &&
                !participant.TryAbortForHostMigrationRecovery())
            {
                throw new InvalidOperationException(
                    $"Unrecovered Raiding participant '{profileId}' could not transition to Aborted.");
            }

            RaidParticipantState finalizedState = participant.State;

            participantObject.AssignInputAuthority(PlayerRef.None);
            if (avatar != null)
            {
                avatar.AssignInputAuthority(PlayerRef.None);
            }

            _controlledReturns.MarkTerminal(
                new ControlledReturnKey(profileId.Value, generationId));

            if (finalizedState != RaidParticipantState.Defeated && avatar != null)
            {
                _runner.Despawn(avatar);
            }

            _runner.Despawn(participantObject);
            _restoredHostMigrationParticipants.Remove(profileId);

            Debug.Log(
                $"[HM-MULTI] Unrecovered participant finalized. ProfileId={profileId}, " +
                $"State={finalizedState}.",
                this);
        }
    }

    private bool ValidateSealedHostMigrationRoster(out string failure)
    {
        var activePlayers = new HashSet<PlayerRef>(_runner.ActivePlayers);
        foreach (KeyValuePair<ProfileId, PlayerRef> recovered in _hostMigrationRecoveredProfiles)
        {
            if (!activePlayers.Contains(recovered.Value) ||
                !_restoredHostMigrationParticipants.TryGetValue(
                    recovered.Key,
                    out NetworkObject participantObject) ||
                participantObject == null ||
                !_runner.TryGetPlayerObject(recovered.Value, out NetworkObject playerObject) ||
                playerObject != participantObject ||
                participantObject.InputAuthority != recovered.Value ||
                !participantObject.TryGetBehaviour(out NetworkRaidParticipant participant))
            {
                failure = $"Recovered profile '{recovered.Key}' has an invalid PlayerObject mapping.";
                return false;
            }

            if (participant.IsReturnAuthorized ||
                (participant.State != RaidParticipantState.Raiding &&
                 participant.State != RaidParticipantState.Defeated))
            {
                failure = $"Recovered profile '{recovered.Key}' is terminal and cannot remain operational.";
                return false;
            }

            if (participant.State == RaidParticipantState.Raiding &&
                (!participant.TryResolveCurrentAvatar(out NetworkObject avatar) ||
                 avatar == null || avatar.InputAuthority != recovered.Value))
            {
                failure = $"Recovered Raiding profile '{recovered.Key}' has invalid avatar authority.";
                return false;
            }

            if (participant.State == RaidParticipantState.Defeated &&
                (!TryFindLinkedAvatarOrCorpse(participantObject.Id, out NetworkObject corpse) ||
                 corpse == null || !corpse.HasStateAuthority ||
                 corpse.InputAuthority != PlayerRef.None ||
                 corpse.GetComponent<NetworkLootContainer>() == null))
            {
                failure = $"Recovered Defeated profile '{recovered.Key}' has an invalid corpse or loot authority.";
                return false;
            }
        }

        foreach (KeyValuePair<ProfileId, NetworkObject> restored in
                 _restoredHostMigrationParticipants)
        {
            if (restored.Value != null &&
                restored.Value.TryGetBehaviour(out NetworkRaidParticipant participant) &&
                participant.State == RaidParticipantState.Raiding &&
                !_hostMigrationRecoveredProfiles.ContainsKey(restored.Key))
            {
                failure = $"Raiding profile '{restored.Key}' has no recovered peer.";
                return false;
            }
        }

        failure = null;
        return true;
    }

    private void RemoveRecoveredHostMigrationProfile(
        ProfileId profileId,
        PlayerRef player,
        bool requeue)
    {
        if (_restoredHostMigrationParticipants.TryGetValue(
                profileId,
                out NetworkObject participantObject) &&
            participantObject != null)
        {
            if (participantObject.TryGetBehaviour(out NetworkRaidParticipant participant) &&
                participant.TryResolveCurrentAvatar(out NetworkObject avatar) &&
                avatar != null)
            {
                avatar.AssignInputAuthority(PlayerRef.None);
            }

            participantObject.AssignInputAuthority(PlayerRef.None);
        }

        if (!player.IsNone)
        {
            bool playerIsActive = false;
            foreach (PlayerRef activePlayer in _runner.ActivePlayers)
            {
                if (activePlayer == player)
                {
                    playerIsActive = true;
                    break;
                }
            }

            if (playerIsActive &&
                _runner.TryGetPlayerObject(player, out NetworkObject mappedObject) &&
                mappedObject != null)
            {
                _runner.SetPlayerObject(player, null);
            }

            _admittedPlayers.Remove(player);
            _spawnedPlayers.Remove(player);
            _spawnedAvatars.Remove(player);
            _admissionData.Remove(player);
        }

        _admittedProfiles.Remove(profileId.Value);
        _hostMigrationRecoveredProfiles.Remove(profileId);
        if (requeue && _hostMigrationEligibleProfiles.Contains(profileId))
        {
            _hostMigrationUnresolvedProfiles.Add(profileId);
        }

        SignalRosterChanged();
    }

    private void TryBindRecoveredLocalPresentation(
        ProfileId profileId,
        PlayerRef player,
        NetworkRaidParticipant participant)
    {
        if (player != _runner.LocalPlayer || participant == null)
        {
            return;
        }

        NetworkObject presentationObject = null;
        if (participant.State == RaidParticipantState.Raiding)
        {
            participant.TryResolveCurrentAvatar(out presentationObject);
        }
        else if (participant.State == RaidParticipantState.Defeated)
        {
            TryFindLinkedAvatarOrCorpse(participant.Object.Id, out presentationObject);
        }

        if (presentationObject == null)
        {
            return;
        }

        presentationObject.GetComponent<LocalPlayerCameraBinder>()?.TryBindAsLocalPlayer();
        presentationObject.GetComponent<LocalPlayerHudBinder>()?.TryBindAsLocalPlayer();
        Debug.Log($"[HM-MULTI] Local presentation rebound. ProfileId={profileId}.", this);
    }

    private bool TryFindLinkedAvatarOrCorpse(
        NetworkId participantId,
        out NetworkObject linkedObject)
    {
        linkedObject = null;
        _cleanupBuffer.Clear();
        _runner.GetAllNetworkObjects(_cleanupBuffer);
        for (int index = 0; index < _cleanupBuffer.Count; index++)
        {
            NetworkObject candidate = _cleanupBuffer[index];
            if (candidate != null &&
                candidate.TryGetBehaviour(out RaidAvatarParticipantLink link) &&
                link.ParticipantId == participantId)
            {
                linkedObject = candidate;
                _cleanupBuffer.Clear();
                return true;
            }
        }

        _cleanupBuffer.Clear();
        return false;
    }

    private static TimeSpan GetRemaining(long deadline)
    {
        long remainingTicks = deadline - System.Diagnostics.Stopwatch.GetTimestamp();
        return remainingTicks <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(
                (double)remainingTicks / System.Diagnostics.Stopwatch.Frequency);
    }

    /// <summary>
    /// Waits for the replacement Client's replicated PlayerObject mapping without
    /// participating in server snapshot restore or authoritative completion.
    /// </summary>
    internal async Task WaitForLocalHostMigrationRecoveryAsync(
        ProfileId expectedProfileId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (_runner == null || _runner.IsServer || !expectedProfileId.IsValid)
        {
            throw new InvalidOperationException(
                "Local Client recovery requires a running non-server replacement and a valid ProfileId.");
        }

        long deadline = System.Diagnostics.Stopwatch.GetTimestamp() +
            (long)(timeout.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);
        while (!TryValidateAndBindLocalHostMigrationRecovery(
                   expectedProfileId,
                   out string failure))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan remaining = GetRemaining(deadline);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(
                    $"Replacement Client local recovery timed out. {failure}");
            }

            Task changed = GetRosterChangedTask();
            Task timeoutTask = Task.Delay(remaining, cancellationToken);
            await Task.WhenAny(changed, timeoutTask);
        }
    }

    internal bool TryValidateAndBindLocalHostMigrationRecovery(
        ProfileId expectedProfileId,
        out string failure)
    {
        if (_runner == null || !_runner.IsRunning || _runner.IsServer ||
            _runner.LocalPlayer.IsNone ||
            !_runner.TryGetPlayerObject(
                _runner.LocalPlayer,
                out NetworkObject participantObject) ||
            participantObject == null ||
            !participantObject.TryGetBehaviour(out NetworkRaidParticipant participant))
        {
            failure = "Local PlayerObject/participant has not replicated yet.";
            return false;
        }

        if (participant.ProfileId.ToString() != expectedProfileId.Value ||
            participantObject.InputAuthority != _runner.LocalPlayer ||
            _matchController == null ||
            !string.Equals(
                participant.RaidGenerationId.ToString(),
                _matchController.RaidGenerationId.ToString(),
                StringComparison.Ordinal))
        {
            failure = "Local ProfileId, generation, or participant authority is inconsistent.";
            return false;
        }

        if (participant.State == RaidParticipantState.Raiding &&
            (!participant.TryResolveCurrentAvatar(out NetworkObject avatar) ||
             avatar == null || avatar.InputAuthority != _runner.LocalPlayer))
        {
            failure = "Local Raiding avatar authority has not replicated yet.";
            return false;
        }

        if (participant.State != RaidParticipantState.Raiding &&
            participant.State != RaidParticipantState.Defeated)
        {
            failure = $"Local participant is terminal ({participant.State}).";
            return false;
        }

        TryBindRecoveredLocalPresentation(expectedProfileId, _runner.LocalPlayer, participant);
        failure = null;
        return true;
    }

    public override void OnObjectEnterAOI(
        NetworkRunner runner,
        NetworkObject networkObject,
        PlayerRef player)
    {
        if (runner == _runner &&
            _startupContext.IsValid &&
            _startupContext.Mode == SessionStartupMode.HostMigrationResume &&
            !runner.IsServer)
        {
            SignalRosterChanged();
        }
    }

    private Task GetRosterChangedTask()
    {
        if (_hostMigrationRosterChanged == null ||
            _hostMigrationRosterChanged.Task.IsCompleted)
        {
            _hostMigrationRosterChanged = CreateRosterSignal();
        }

        return _hostMigrationRosterChanged.Task;
    }

    private void SignalRosterChanged()
    {
        _hostMigrationRosterChanged?.TrySetResult(true);
    }

    private static TaskCompletionSource<bool> CreateRosterSignal()
    {
        return new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void CompleteHostMigration(
        HostMigrationCompletionStatus status,
        string details)
    {
        TaskCompletionSource<HostMigrationCompletionResult> completion =
            _hostMigrationCompletion;
        if (completion == null)
        {
            return;
        }

        var result = new HostMigrationCompletionResult(status, details);
        if (!completion.TrySetResult(result))
        {
            Debug.LogWarning(
                $"[HM-MULTI] Ignored duplicate migration completion. " +
                $"Attempted={status}, Existing={completion.Task.Result.Status}.",
                this);
            return;
        }

        Debug.Log(
            $"[HM-MULTI] Runtime rebind completion={status}. {details}",
            this);
    }

    private HostMigrationCompletionResult CreateHostMigrationTimeoutResult()
    {
        Scene runnerScene = _runner != null && _runner.SceneManager != null
            ? _runner.SceneManager.MainRunnerScene
            : default;
        string sceneState = runnerScene.IsValid()
            ? $"{runnerScene.name}:loaded={runnerScene.isLoaded}"
            : "unavailable";
        string matchPhase = "unavailable";
        if (_matchController != null && _matchController.Object != null &&
            _matchController.Object.IsValid)
        {
            matchPhase = _matchController.Phase.ToString();
        }

        string details =
            $"RunnerRunning={_runner != null && _runner.IsRunning}, " +
            $"RunnerServer={_runner != null && _runner.IsServer}, " +
            $"Scene={sceneState}, " +
            $"SnapshotReported={_snapshotRestoreReported}, " +
            $"SnapshotSucceeded={_snapshotRestoreSucceeded}, " +
            $"RebindState={_sceneLoadState}, MatchPhase={matchPhase}.";
        return new HostMigrationCompletionResult(
            HostMigrationCompletionStatus.Timeout,
            details);
    }

    public override void OnConnectRequest(
        NetworkRunner runner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
        if (!runner.IsServer || runner != _runner)
            return;

        bool isHostMigrationResume =
            _startupContext.IsValid &&
            _startupContext.Mode == SessionStartupMode.HostMigrationResume;
        if (isHostMigrationResume)
        {
            if (_hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Sealing ||
                _hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Closed ||
                _hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Failed ||
                _launchContext == null ||
                !TryValidateEarlyHostMigrationToken(
                    token,
                    out RaidAdmissionData migrationAdmission) ||
                migrationAdmission.ProfileId == _launchContext.HostProfileId)
            {
                Debug.LogWarning(
                    "[HM-MULTI] Refusing reconnect outside the open recovery boundary or for an invalid profile.",
                    this);
                request.Refuse();
            }

            return;
        }

        if (_launchContext != null && !TryValidateRaidAdmissionToken(token, out _))
        {
            Debug.LogWarning("[NetworkSpawnManager] Refusing connection request with an invalid raid admission token.");
            request.Refuse();
            return;
        }

        bool allowJoin = _matchController != null &&
                         _matchController.Phase == NetworkMatchController.MatchPhase.WaitingForPlayers;

        if (!allowJoin)
        {
            Debug.LogWarning($"[NetworkSpawnManager] Refusing connection request from {request.RemoteAddress} because the match has already started (Phase: {_matchController?.Phase}) and it's not a valid Host Migration window.");
            request.Refuse();
        }
    }

    public override void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"PlayerJoined: {player}");

        if (_startupContext.IsValid && _startupContext.Mode == SessionStartupMode.HostMigrationResume)
        {
            Debug.Log($"[HM-MULTI] Replacement player joined: PlayerRef={player}.", this);
        }

        if (!runner.IsServer || runner != _runner)
        {
            if (runner == _runner &&
                _startupContext.IsValid &&
                _startupContext.Mode == SessionStartupMode.HostMigrationResume)
            {
                SignalRosterChanged();
            }

            return;
        }

        if (_startupContext.IsValid &&
            _startupContext.Mode == SessionStartupMode.HostMigrationResume)
        {
            HandleHostMigrationPlayerJoined(runner, player);
            return;
        }

        // Reject remote connections if coordinator is not ready
        if (_matchController == null)
        {
            if (player == runner.LocalPlayer)
            {
                Debug.Log("[NetworkSpawnManager] OnPlayerJoined: Coordinator not ready, deferring local Host admission.");
                return;
            }
            else
            {
                Debug.LogWarning("[NetworkSpawnManager] OnPlayerJoined: Coordinator not ready. Disconnecting remote player.");
                runner.Disconnect(player);
                return;
            }
        }

        // Try to admit player
        if (TryAdmitPlayer(runner, player))
        {
            if (_spawnedPlayers.ContainsKey(player))
            {
                Debug.Log($"[NetworkSpawnManager] Player {player} joined and is already spawned (restored). Skipping SpawnPlayer.");
            }
            else
            {
                if (!SpawnPlayer(runner, player))
                {
                    RemoveAdmissionRecord(player);
                    if (player != runner.LocalPlayer)
                    {
                        runner.Disconnect(player);
                    }
                }
            }

            return;
        }

        if (player != runner.LocalPlayer)
        {
            Debug.LogWarning($"[NetworkSpawnManager] Disconnecting player {player} after admission was rejected.");
            runner.Disconnect(player);
        }
        else
        {
            Debug.LogError("[NetworkSpawnManager] Local Host admission was rejected.");
        }
    }

    public bool TryAdmitPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer || runner != _runner)
            return false;

        if (player.IsNone)
            return false;

        if (_startupContext.IsValid &&
            _startupContext.Mode == SessionStartupMode.HostMigrationResume)
        {
            return false;
        }

        if (_matchController == null)
            return false;

        if (!TryGetJoinData(runner, player, out PlayerJoinData joinData))
        {
            return false;
        }

        RaidAdmissionData admission = default;
        bool hasAdmission = _launchContext != null;
        if (hasAdmission && !TryGetAdmissionData(runner, player, out admission))
        {
            return false;
        }

        var admissionKey = new ControlledReturnKey(
            joinData.ProfileId.Value,
            _matchController.RaidGenerationId.ToString());
        if (_controlledReturns.IsTerminal(in admissionKey))
        {
            Debug.LogWarning($"[NetworkSpawnManager] Rejected departed profile '{joinData.ProfileId.Value}' from re-entering this raid.");
            return false;
        }

        if (_admittedPlayers.Contains(player))
            return true;

        string profileKey = joinData.ProfileId.Value;
        if (_admittedProfiles.TryGetValue(profileKey, out PlayerRef admittedProfilePlayer) && admittedProfilePlayer != player)
        {
            Debug.LogWarning($"[NetworkSpawnManager] Rejected duplicate admitted profile '{profileKey}'.");
            return false;
        }

        bool admissionPhaseIsOpen = _matchController.Phase == NetworkMatchController.MatchPhase.WaitingForPlayers;
        if (!admissionPhaseIsOpen)
            return false;

        _admittedPlayers.Add(player);
        _admittedProfiles[profileKey] = player;
        if (hasAdmission)
        {
            _admissionData[player] = admission;
        }
        Debug.Log($"[NetworkSpawnManager] Player {player} registered as an admitted participant.");
        return true;
    }

    private void HandleHostMigrationPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        bool acceptingArrivals =
            _hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Inactive ||
            _hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Open;
        if (!acceptingArrivals)
        {
            if (player != runner.LocalPlayer)
            {
                runner.Disconnect(player);
            }

            return;
        }

        if (TryGetJoinData(runner, player, out PlayerJoinData resolvedJoinData) &&
            _hostMigrationRecoveredProfiles.TryGetValue(
                resolvedJoinData.ProfileId,
                out PlayerRef recoveredPlayer))
        {
            if (recoveredPlayer == player)
            {
                SignalRosterChanged();
                return;
            }

            if (player != runner.LocalPlayer)
            {
                runner.Disconnect(player);
            }
            else
            {
                FailHostMigrationRecovery(
                    "Local replacement arrival conflicts with an already recovered ProfileId.");
            }

            return;
        }

        if (!TryGetJoinData(runner, player, out PlayerJoinData joinData) ||
            !joinData.ProfileId.IsValid ||
            _launchContext == null ||
            joinData.ProfileId == _launchContext.HostProfileId ||
            !RaidSessionRules.ContainsProfile(
                _launchContext.ParticipantProfileIds,
                joinData.ProfileId) ||
            (_earlyHostMigrationReconnects.TryGetValue(
                 joinData.ProfileId,
                 out PlayerRef existingPlayer) && existingPlayer != player))
        {
            Debug.LogWarning(
                $"[HM-MULTI] Rejected replacement arrival PlayerRef={player}.",
                this);
            if (player != runner.LocalPlayer)
            {
                runner.Disconnect(player);
            }
            else
            {
                FailHostMigrationRecovery(
                    "Local replacement arrival has invalid recovery identity data.");
            }

            return;
        }

        _earlyHostMigrationReconnects[joinData.ProfileId] = player;
        if (_hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Open)
        {
            TryRebindHostMigrationProfile(joinData.ProfileId, player);
        }

        Debug.Log(
            $"[HM-MULTI] Queued recovery arrival ProfileId={joinData.ProfileId.Value}, PlayerRef={player}, State={_hostMigrationRecoveryState}.",
            this);
        SignalRosterChanged();
    }

    private void FailHostMigrationRecovery(string details)
    {
        _hostMigrationRecoveryState = HostMigrationRecoveryWindowState.Failed;
        _spawnsBlocked = true;
        CompleteHostMigration(HostMigrationCompletionStatus.Failure, details);
        SignalRosterChanged();
    }

    public void NotifyPendingReconnectCharacterDefeated(NetworkObject obj)
    {
        if (obj == null || !IsHostMigrationRecoveryInProgress ||
            !obj.TryGetBehaviour(out RaidAvatarParticipantLink link) ||
            !link.TryResolveParticipant(out NetworkRaidParticipant participant))
        {
            return;
        }

        ProfileId profileId = new ProfileId(participant.ProfileId.ToString());
        if (_hostMigrationRecoveredProfiles.TryGetValue(
                profileId,
                out PlayerRef player))
        {
            _spawnedAvatars.Remove(player);
        }

        // Defeat changes the recovered shape from avatar gameplay authority to
        // participant-only spectator authority while preserving the corpse object.
        SignalRosterChanged();
    }

    internal void NotifyHostMigrationAuthorityChanged()
    {
        if (_startupContext.IsValid &&
            _startupContext.Mode == SessionStartupMode.HostMigrationResume)
        {
            SignalRosterChanged();
        }
    }

    public HostBootstrapResult TryBootstrapHost(
        NetworkRunner runner,
        NetworkMatchController matchController)
    {
        if (runner == null || _runner != runner)
            return HostBootstrapResult.InvalidRunner;

        if (!runner.IsServer)
            return HostBootstrapResult.NoAuthority;

        if (runner.LocalPlayer.IsNone)
            return HostBootstrapResult.InvalidRunner;

        if (matchController == null || matchController.Phase != NetworkMatchController.MatchPhase.WaitingForPlayers)
            return HostBootstrapResult.InvalidCoordinator;

        // Try to admit the Host player idempotently
        if (!TryAdmitPlayer(runner, runner.LocalPlayer))
            return HostBootstrapResult.AdmissionFailed;

        // If Host character is already spawned, bootstrap is complete
        if (_spawnedPlayers.ContainsKey(runner.LocalPlayer))
            return HostBootstrapResult.BootstrapCompleted;

        // Verify if we have a valid completed loading state and spatial configuration
        if (_spawnsBlocked || _sceneLoadState != SceneLoadProcessingState.Completed || _sceneSpawnStatus != SceneSpawnConfigurationStatus.SpawnPointsReady)
        {
            Debug.Log("[NetworkSpawnManager] Host admitted, but player spawn is pending scene load.");
            return HostBootstrapResult.HostAdmittedSpawnPending;
        }

        // Spawn points are configured, spawn immediately
        if (!SpawnPlayer(runner, runner.LocalPlayer))
        {
            RemoveAdmissionRecord(runner.LocalPlayer);
            return HostBootstrapResult.AdmissionFailed;
        }
        if (_spawnedPlayers.ContainsKey(runner.LocalPlayer))
        {
            return HostBootstrapResult.BootstrapCompleted;
        }

        return HostBootstrapResult.HostAdmittedSpawnPending;
    }

    private bool TryGetJoinData(
        NetworkRunner runner,
        PlayerRef player,
        out PlayerJoinData joinData)
    {
        byte[] token = runner.GetPlayerConnectionToken(player);

        if (_launchContext != null && RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData raidAdmission))
        {
            if (IsRaidAdmissionValid(raidAdmission))
            {
                joinData = raidAdmission.ToPlayerJoinData();
                return true;
            }

            joinData = default;
            return false;
        }

        if (PlayerJoinDataCodec.TryDecode(token, out joinData))
        {
            return true;
        }

        if (!runner.IsServer || player != runner.LocalPlayer)
        {
            joinData = default;
            return false;
        }

        LocalPlayerJoinContext context = runner.GetComponent<LocalPlayerJoinContext>();

        if (context == null || !PlayerJoinDataCodec.IsSupported(context.JoinData.ClassId) ||
            (_launchContext != null && (!context.HasRaidAdmission || !IsRaidAdmissionValid(context.RaidAdmission))))
        {
            joinData = default;
            return false;
        }

        joinData = context.JoinData;
        return true;
    }

    private bool TryGetAdmissionData(
        NetworkRunner runner,
        PlayerRef player,
        out RaidAdmissionData admission)
    {
        admission = default;
        byte[] token = runner.GetPlayerConnectionToken(player);
        if (!RaidAdmissionDataCodec.TryDecode(token, out admission) || !IsRaidAdmissionValid(admission))
        {
            return false;
        }

        if (!RaidLoadoutRules.TryValidate(
                admission.ReservedLoadout,
                _lootCatalog,
                LocalProfileSnapshot.MaxLoadoutSlots,
                out string validationError))
        {
            Debug.LogWarning($"[NetworkSpawnManager] Rejected loadout for profile '{admission.ProfileId.Value}': {validationError}.");
            return false;
        }

        return true;
    }

    public int ExpectedRaidAdmissionCount =>
        _launchContext != null
            ? _launchContext.ParticipantProfileIds.Count
            : 0;
    public int AdmittedRaidProfileCount => _admittedProfiles.Count;
    public int ReadyRaidProfileCount => _spawnedPlayers.Count;

    private bool TryValidateRaidAdmissionToken(byte[] token, out RaidAdmissionData admission)
    {
        return RaidAdmissionDataCodec.TryDecode(token, out admission) &&
               IsRaidAdmissionValid(admission) &&
               RaidLoadoutRules.TryValidate(
                   admission.ReservedLoadout,
                   _lootCatalog,
                   LocalProfileSnapshot.MaxLoadoutSlots,
                   out _);
    }

    private bool TryValidateEarlyHostMigrationToken(
        byte[] token,
        out RaidAdmissionData admission)
    {
        return RaidAdmissionDataCodec.TryDecode(token, out admission) &&
               admission.ProfileId.IsValid &&
               IsRaidAdmissionValid(admission) &&
               RaidSessionRules.ContainsProfile(
                   _launchContext.ParticipantProfileIds,
                   admission.ProfileId);
    }

    private bool IsRaidAdmissionValid(in RaidAdmissionData admission)
    {
        return RaidAdmissionRules.IsAdmitted(_launchContext, admission);
    }

    private bool CanSpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (runner != _runner)
            return false;

        if (!runner.IsServer)
            return false;

        if (player.IsNone)
            return false;

        // Allow spawning during Processing (only if it's the internal pipeline doing it)
        // or when Completed with a valid spatial configuration
        bool loadStateValid = _sceneLoadState == SceneLoadProcessingState.Processing || 
                              (_sceneLoadState == SceneLoadProcessingState.Completed && _sceneSpawnStatus == SceneSpawnConfigurationStatus.SpawnPointsReady);
        if (!loadStateValid)
            return false;

        if (_matchController == null || _matchController.Runner != runner)
            return false;

        if (!_admittedPlayers.Contains(player))
            return false;

        if (_spawnedPlayers.ContainsKey(player))
            return false;

        if (runner.GetPlayerObject(player) != null)
            return false;

        if (_playerClassCatalog == null)
            return false;

        if (!_raidParticipantPrefab.IsValid)
            return false;

        bool phaseAllowsSpawning = _matchController.Phase == NetworkMatchController.MatchPhase.WaitingForPlayers ||
                                   _matchController.Phase == NetworkMatchController.MatchPhase.Starting ||
                                   _matchController.Phase == NetworkMatchController.MatchPhase.InProgress;

        return phaseAllowsSpawning;
    }

    private void RemoveAdmissionRecord(PlayerRef player)
    {
        _admittedPlayers.Remove(player);
        _admissionData.Remove(player);

        string profileToRemove = null;
        foreach (var pair in _admittedProfiles)
        {
            if (pair.Value == player)
            {
                profileToRemove = pair.Key;
                break;
            }
        }

        if (profileToRemove != null)
        {
            _admittedProfiles.Remove(profileToRemove);
        }
    }

    private bool SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (_startupContext.IsValid && _startupContext.Mode == SessionStartupMode.HostMigrationResume)
        {
            Debug.LogWarning(
                $"[HM-MULTI] Fresh SpawnPlayer rejected during recovery. PlayerRef={player}.",
                this);
        }

        if (!CanSpawnPlayer(runner, player))
        {
            Debug.LogWarning($"[NetworkSpawnManager] Rejecting spawn for player {player}: validation failed.");
            return false;
        }

        if (!TryGetJoinData(runner, player, out PlayerJoinData joinData))
        {
            Debug.LogError($"Rejecting spawn for player {player}: Invalid or missing join data.");
            return false;
        }

        if (!_playerClassCatalog.TryGetPrefab(joinData.ClassId, out NetworkPrefabRef prefab))
        {
            Debug.LogError($"Rejecting spawn for player {player}: Class {joinData.ClassId} not registered.");
            return false;
        }

        _admissionData.TryGetValue(player, out RaidAdmissionData admission);
        bool hasAdmission = _launchContext != null;
        bool loadoutInitialized = !hasAdmission;

        if (_launchContext == null ||
            !RaidParticipantSpawnRules.TryGetSpawnIndex(
                _launchContext.ParticipantProfileIds,
                joinData.ProfileId,
                out int spawnIndex) ||
            !TryGetSpawnTransformByIndex(
                SpawnGroupType.Players,
                spawnIndex,
                out Vector3 position,
                out Quaternion rotation))
        {
            Debug.LogError(
                $"Rejecting spawn for profile '{joinData.ProfileId.Value}': frozen spawn mapping is unavailable.");
            return false;
        }

        NetworkObject participantObject = runner.Spawn(
            _raidParticipantPrefab,
            position,
            rotation,
            player,
            (r, obj) => {
                if (obj.TryGetBehaviour(out NetworkRaidParticipant participant))
                {
                    participant.Initialize(
                        joinData.ProfileId.Value,
                        joinData.ClassId,
                        _matchController != null ? _matchController.RaidGenerationId.ToString() : null,
                        hasAdmission ? admission.ReservationId : null);
                }
            });

        if (participantObject == null || !participantObject.TryGetBehaviour(out NetworkRaidParticipant raidParticipant))
        {
            if (participantObject != null)
            {
                runner.Despawn(participantObject);
            }

            Debug.LogError($"Rejecting spawn for player {player}: participant prefab is missing {nameof(NetworkRaidParticipant)}.");
            return false;
        }

        NetworkObject avatarObject = runner.Spawn(
            prefab,
            position,
            rotation,
            player,
            (r, obj) => {
                if (obj.TryGetBehaviour(out PlayerCharacter playerCharacter))
                {
                    playerCharacter.ProfileIdString = joinData.ProfileId.Value;
                }

                if (obj.TryGetBehaviour(out RaidAvatarParticipantLink avatarLink))
                {
                    avatarLink.Initialize(participantObject);
                }

                if (hasAdmission)
                {
                    if (!obj.TryGetBehaviour(out PlayerLootReceiver lootReceiver))
                    {
                        loadoutInitialized = false;
                        return;
                    }

                    loadoutInitialized = lootReceiver.TryInitializeLoadout(
                        admission.ReservedLoadout,
                        out string loadoutError);
                    if (!loadoutInitialized)
                    {
                        Debug.LogError($"[NetworkSpawnManager] Failed to initialize loadout for player {player}: {loadoutError}.", obj);
                    }
                }
            });

        if (avatarObject == null || !avatarObject.TryGetBehaviour(out RaidAvatarParticipantLink _))
        {
            if (avatarObject != null)
            {
                runner.Despawn(avatarObject);
            }

            runner.Despawn(participantObject);
            Debug.LogError($"Rejecting spawn for player {player}: avatar prefab is missing {nameof(RaidAvatarParticipantLink)}.");
            return false;
        }

        if (!loadoutInitialized)
        {
            runner.Despawn(avatarObject);
            runner.Despawn(participantObject);
            return false;
        }

        if (!raidParticipant.TrySetCurrentAvatar(avatarObject))
        {
            runner.Despawn(avatarObject);
            runner.Despawn(participantObject);
            Debug.LogError($"Rejecting spawn for player {player}: participant initialization failed.");
            return false;
        }

        runner.SetPlayerObject(player, participantObject);

        _spawnedPlayers.Add(player, participantObject);
        _spawnedAvatars.Add(player, avatarObject);

        Debug.Log($"Spawned participant and avatar for player {player} with class {joinData.ClassId}.");
        return true;
    }

    /// <summary>
    /// Aborts every currently active participant for a Host-requested raid closure.
    /// </summary>
    public void AbortRaidingParticipantsForClosure()
    {
        if (_runner == null || !_runner.IsServer)
        {
            return;
        }

        foreach (NetworkObject participantObject in _spawnedPlayers.Values)
        {
            if (participantObject != null &&
                participantObject.TryGetBehaviour(out NetworkRaidParticipant participant))
            {
                participant.TryAbortForClosure();
            }
        }
    }

    /// <summary>
    /// Despawns every network object owned by this runner except the lifecycle coordinator.
    /// Runner scope is the generation boundary: a different raid always owns a new runner.
    /// </summary>
    public bool TryCleanupRaidGeneration(out int failureCount)
    {
        failureCount = 0;
        if (_runner == null || !_runner.IsServer)
        {
            failureCount = 1;
            return false;
        }

        _spawnsBlocked = true;
        _cleanupBuffer.Clear();
        _runner.GetAllNetworkObjects(_cleanupBuffer);

        for (int index = 0; index < _cleanupBuffer.Count; index++)
        {
            NetworkObject networkObject = _cleanupBuffer[index];
            if (networkObject == null ||
                networkObject.TryGetBehaviour(out NetworkMatchController _) ||
                networkObject.NetworkTypeId.IsSceneObject)
            {
                continue;
            }

            try
            {
                _runner.Despawn(networkObject);
            }
            catch (Exception exception)
            {
                failureCount++;
                Debug.LogException(exception, networkObject);
            }
        }

        _spawnedPlayers.Clear();
        _spawnedAvatars.Clear();
        _admissionData.Clear();
        _spawnedEnemies.Clear();
        _admittedPlayers.Clear();
        _admittedProfiles.Clear();
        _controlledReturns.Clear();
        _lootSpawnState.Clear();
        _breakableSpawnState.Clear();
        _lootSessionSeed = 0;
        _hasLootSessionSeed = false;
        _spawnPointLookup.Clear();
        _sceneSpawnPointConfiguration = null;
        _sceneSpawnStatus = SceneSpawnConfigurationStatus.None;
        _sceneLoadState = SceneLoadProcessingState.None;
        _runner.GetComponent<EntityRegistry>()?.ClearForRaidClosure();
        _runner.GetComponent<ExtractionSanctuaryAssignmentService>()?.ResetForRaidClosure();
        _cleanupBuffer.Clear();

        return failureCount == 0;
    }

    private bool SpawnEnemy(NetworkRunner runner)
    {
        if (_enemyPrefabs == null || _enemyPrefabs.Length <= 0)
        {
            Debug.LogError("Cannot spawn enemy: Enemy prefab reference is missing.");
            return false;
        }
        GetSpawnTransform(
            SpawnGroupType.Enemies,
            UnityEngine.Random.Range(0, int.MaxValue),
            out Vector3 position,
            out Quaternion rotation);
        NetworkObject enemyObject = runner.Spawn(
            _enemyPrefabs[UnityEngine.Random.Range(0, _enemyPrefabs.Length)],
            position,
            rotation);

        if (enemyObject == null)
        {
            Debug.LogError("Cannot spawn enemy: Fusion returned a null NetworkObject.", this);
            return false;
        }

        _spawnedEnemies.Add(enemyObject);
        Debug.Log($"Spawned enemy at {position}.");
        return true;
    }

    private bool SpawnConfiguredLootContainers(NetworkRunner runner, SpawnGroupDefinition definition)
    {
        if (!_lootContainerPrefab.IsValid)
        {
            Debug.LogError(
                "[NetworkSpawnManager] Loot group skipped because LootContainer.prefab is not configured on the Gameplay scene manager.",
                this);
            return false;
        }

        if (!TryPrepareLootContentSnapshot(
                runner,
                out ValidatedLootContainerContentSnapshot snapshot,
                out string preparationError))
        {
            Debug.LogError(
                $"[NetworkSpawnManager] Loot group skipped because its random-content configuration is invalid. {preparationError}",
                this);
            return false;
        }

        if (!EnsureLootSessionSeed(runner))
        {
            Debug.LogError("[NetworkSpawnManager] Loot group skipped because a server-owned session seed could not be created.", this);
            return false;
        }

        bool completed = true;

        int spawnCount = InitialSpawnGroupPolicy.GetLootSpawnCount(definition, out bool wasClamped);
        if (wasClamped)
        {
            Debug.LogWarning(
                $"[NetworkSpawnManager] Loot group requested {definition.Amount} containers but has only {definition.SpawnPoints.Length} points. Spawning was limited to {spawnCount}.",
                this);
        }

        for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
        {
            if (_lootSpawnState.ContainsPoint(spawnIndex))
            {
                continue;
            }

            ulong containerSeed = LootContainerSeedRules.Derive(
                _lootSessionSeed,
                _currentSceneLoadGeneration,
                (int)SpawnGroupType.Loot,
                spawnIndex);
            if (!LootContainerContentRoller.TryRoll(
                    snapshot,
                    containerSeed,
                    out IReadOnlyList<LootEntry> rolledContent,
                    out string rollError))
            {
                Debug.LogError(
                    $"[NetworkSpawnManager] Loot roll failed for point {spawnIndex}, generation {_currentSceneLoadGeneration}, seed {containerSeed}. {rollError}",
                    this);
                continue;
            }

            NetworkObject lootContainer = SpawnLootContainer(
                runner,
                SpawnGroupType.Loot,
                spawnIndex,
                containerSeed,
                rolledContent,
                out bool fatalIntegrationFailure);
            if (lootContainer == null)
            {
                if (fatalIntegrationFailure)
                {
                    completed = false;
                    break;
                }

                continue;
            }

            _lootSpawnState.TryRecordSuccessfulSpawn(spawnIndex, lootContainer);
        }

        return completed;
    }

    private NetworkObject SpawnLootContainer(
        NetworkRunner runner,
        SpawnGroupType group,
        int spawnIndex,
        ulong containerSeed,
        IReadOnlyList<LootEntry> rolledContent,
        out bool fatalIntegrationFailure)
    {
        fatalIntegrationFailure = false;
        if (runner == null || runner != _runner || !runner.IsServer ||
            group != SpawnGroupType.Loot || !_lootContainerPrefab.IsValid || rolledContent == null)
        {
            return null;
        }

        GetSpawnTransform(group, spawnIndex, out Vector3 position, out Quaternion rotation);
        bool callbackApplied = false;
        NetworkObject callbackObject = null;
        NetworkLootContainer callbackContainer = null;
        NetworkObject lootContainer = runner.Spawn(
            _lootContainerPrefab,
            position,
            rotation,
            inputAuthority: null,
            onBeforeSpawned: (callbackRunner, instance) =>
            {
                callbackObject = instance;
                callbackContainer = instance != null
                    ? instance.GetComponent<NetworkLootContainer>()
                    : null;
                callbackApplied = callbackContainer != null &&
                    callbackContainer.TrySetInitialContentOverride(
                        callbackRunner,
                        instance,
                        rolledContent);
            });

        if (lootContainer == null)
        {
            Debug.LogError(
                $"[NetworkSpawnManager] Loot container spawn failed at point {spawnIndex}, position {position}.",
                this);
            return null;
        }

        bool initializedSuccessfully = lootContainer.Id.IsValid &&
            ReferenceEquals(callbackObject, lootContainer) &&
            callbackContainer != null &&
            callbackContainer.Object == lootContainer &&
            callbackApplied &&
            callbackContainer.IsInitialized &&
            callbackContainer.IsAvailable;

        if (!initializedSuccessfully)
        {
            Debug.LogError(
                $"[NetworkSpawnManager] Loot container initialization failed at point {spawnIndex}, position {position}, seed {containerSeed}. " +
                $"objectValid={lootContainer.Id.IsValid}, callbackApplied={callbackApplied}, " +
                $"initialized={callbackContainer != null && callbackContainer.IsInitialized}, " +
                $"available={callbackContainer != null && callbackContainer.IsAvailable}. The instance will be despawned.",
                lootContainer);

            if (lootContainer.Id.IsValid)
            {
                NetworkId spawnedId = lootContainer.Id;
                try
                {
                    runner.Despawn(lootContainer);
                    if (runner.TryFindObject(spawnedId, out NetworkObject remainingObject) &&
                        ReferenceEquals(remainingObject, lootContainer))
                    {
                        fatalIntegrationFailure = true;
                        Debug.LogError(
                            $"[NetworkSpawnManager] Compensating despawn did not remove loot object {spawnedId}. Remaining Loot points will not be processed.",
                            lootContainer);
                    }
                }
                catch (Exception exception)
                {
                    fatalIntegrationFailure = true;
                    Debug.LogException(exception, lootContainer);
                    Debug.LogError(
                        $"[NetworkSpawnManager] Compensating despawn failed for loot object {spawnedId}. Remaining Loot points will not be processed.",
                        lootContainer);
                }
            }

            return null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[NetworkSpawnManager] Loot roll generation={_currentSceneLoadGeneration}, point={spawnIndex}, sessionSeed={_lootSessionSeed}, containerSeed={containerSeed}.",
            lootContainer);
#endif
        Debug.Log(
            $"[NetworkSpawnManager] Spawned loot container at point {spawnIndex}, position {position}.",
            lootContainer);
        return lootContainer;
    }

    private bool TryPrepareLootContentSnapshot(
        NetworkRunner runner,
        out ValidatedLootContainerContentSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = null;

        if (runner == null || runner != _runner || !runner.IsServer || runner.Config == null)
        {
            error = "Runner is missing, mismatched, or lacks server authority.";
            return false;
        }

        NetworkPrefabId prefabId = runner.Config.PrefabTable.GetId((NetworkObjectGuid)_lootContainerPrefab);
        if (!prefabId.IsValid)
        {
            error = "The configured loot prefab is not registered in Fusion's prefab table.";
            return false;
        }

        NetworkObject prefabObject = runner.Config.PrefabTable.Load(prefabId, true);
        if (prefabObject == null)
        {
            error = "Fusion could not synchronously resolve the configured loot prefab.";
            return false;
        }

        NetworkLootContainer container = prefabObject.GetComponent<NetworkLootContainer>();
        LootContainerRandomContentConfig randomConfig = prefabObject.GetComponent<LootContainerRandomContentConfig>();
        if (container == null)
        {
            error = "The configured loot prefab has no NetworkLootContainer on its root.";
            return false;
        }

        if (randomConfig == null || !randomConfig.enabled)
        {
            error = "The configured loot prefab has no enabled random-content configuration on its root.";
            return false;
        }

        if (!container.StartsAvailable)
        {
            error = "The production loot prefab must start available after successful initialization.";
            return false;
        }

        return LootContainerContentTableValidation.TryCreateSnapshot(
            randomConfig.Table,
            container.LootCatalog,
            container.SlotCapacity,
            NetworkLootContainer.MaxLootTypes,
            out snapshot,
            out error);
    }

    private bool EnsureLootSessionSeed(NetworkRunner runner)
    {
        if (_hasLootSessionSeed)
        {
            return true;
        }

        if (runner == null || runner != _runner || !runner.IsServer)
        {
            return false;
        }

        var bytes = new byte[sizeof(ulong)];
        using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
        {
            generator.GetBytes(bytes);
        }

        _lootSessionSeed = BitConverter.ToUInt64(bytes, 0);
        _hasLootSessionSeed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkSpawnManager] Created authoritative loot session seed {_lootSessionSeed}.", this);
#endif
        return true;
    }

    private bool SpawnConfiguredBreakables(NetworkRunner runner, SpawnGroupDefinition definition)
    {
        if (!_breakablePrefab.IsValid)
        {
            Debug.LogError(
                "[NetworkSpawnManager] Breakables group skipped because its network prefab is not configured.",
                this);
            return false;
        }

        if (!TryPrepareBreakableContentSnapshot(
                runner,
                out ValidatedLootContainerContentSnapshot snapshot,
                out string preparationError))
        {
            Debug.LogError(
                $"[NetworkSpawnManager] Breakables group skipped because its configuration is invalid. {preparationError}",
                this);
            return false;
        }

        if (!EnsureLootSessionSeed(runner))
        {
            Debug.LogError(
                "[NetworkSpawnManager] Breakables group skipped because a server-owned session seed could not be created.",
                this);
            return false;
        }

        bool completed = true;

        int spawnCount = InitialSpawnGroupPolicy.GetPointBoundedSpawnCount(
            definition,
            out bool wasClamped);
        if (wasClamped)
        {
            Debug.LogWarning(
                $"[NetworkSpawnManager] Breakables group requested {definition.Amount} objects but has only {definition.SpawnPoints.Length} points. Spawning was limited to {spawnCount}.",
                this);
        }

        for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
        {
            if (_breakableSpawnState.ContainsPoint(spawnIndex))
            {
                continue;
            }

            ulong dropSeed = LootContainerSeedRules.Derive(
                _lootSessionSeed,
                _currentSceneLoadGeneration,
                (int)SpawnGroupType.Breakables,
                spawnIndex);
            if (!LootContainerContentRoller.TryRoll(
                    snapshot,
                    dropSeed,
                    out IReadOnlyList<LootEntry> rolledDrops,
                    out string rollError))
            {
                Debug.LogError(
                    $"[NetworkSpawnManager] Breakable loot roll failed for point {spawnIndex}, generation {_currentSceneLoadGeneration}, seed {dropSeed}. {rollError}",
                    this);
                continue;
            }

            NetworkObject breakableObject = SpawnBreakable(
                runner,
                spawnIndex,
                dropSeed,
                rolledDrops,
                out bool fatalIntegrationFailure);
            if (breakableObject == null)
            {
                if (fatalIntegrationFailure)
                {
                    completed = false;
                    break;
                }

                continue;
            }

            _breakableSpawnState.TryRecordSuccessfulSpawn(spawnIndex, breakableObject);
        }

        return completed;
    }

    private NetworkObject SpawnBreakable(
        NetworkRunner runner,
        int spawnIndex,
        ulong dropSeed,
        IReadOnlyList<LootEntry> rolledDrops,
        out bool fatalIntegrationFailure)
    {
        fatalIntegrationFailure = false;
        if (runner == null || runner != _runner || !runner.IsServer ||
            !_breakablePrefab.IsValid || rolledDrops == null)
        {
            return null;
        }

        GetSpawnTransform(
            SpawnGroupType.Breakables,
            spawnIndex,
            out Vector3 position,
            out Quaternion rotation);
        bool callbackApplied = false;
        NetworkObject callbackObject = null;
        BreakableObject callbackBreakable = null;
        NetworkObject breakableObject = runner.Spawn(
            _breakablePrefab,
            position,
            rotation,
            inputAuthority: null,
            onBeforeSpawned: (callbackRunner, instance) =>
            {
                callbackObject = instance;
                callbackBreakable = instance != null
                    ? instance.GetComponent<BreakableObject>()
                    : null;
                callbackApplied = callbackBreakable != null &&
                    callbackBreakable.TrySetInitialDropsOverride(
                        callbackRunner,
                        instance,
                        rolledDrops);
            });

        if (breakableObject == null)
        {
            Debug.LogError(
                $"[NetworkSpawnManager] Breakable spawn failed at point {spawnIndex}, position {position}.",
                this);
            return null;
        }

        bool initializedSuccessfully = breakableObject.Id.IsValid &&
            ReferenceEquals(callbackObject, breakableObject) &&
            callbackBreakable != null &&
            callbackBreakable.Object == breakableObject &&
            callbackApplied &&
            callbackBreakable.HasInitialDrops;
        if (!initializedSuccessfully)
        {
            Debug.LogError(
                $"[NetworkSpawnManager] Breakable initialization failed at point {spawnIndex}, position {position}, seed {dropSeed}. The instance will be despawned.",
                breakableObject);
            CompensateFailedSpawn(
                runner,
                breakableObject,
                "breakable",
                ref fatalIntegrationFailure);
            return null;
        }

        Debug.Log(
            $"[NetworkSpawnManager] Spawned breakable at point {spawnIndex}, position {position}.",
            breakableObject);
        return breakableObject;
    }

    private bool TryPrepareBreakableContentSnapshot(
        NetworkRunner runner,
        out ValidatedLootContainerContentSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = null;

        if (runner == null || runner != _runner || !runner.IsServer || runner.Config == null)
        {
            error = "Runner is missing, mismatched, or lacks server authority.";
            return false;
        }

        NetworkPrefabId prefabId = runner.Config.PrefabTable.GetId((NetworkObjectGuid)_breakablePrefab);
        if (!prefabId.IsValid)
        {
            error = "The configured breakable prefab is not registered in Fusion's prefab table.";
            return false;
        }

        NetworkObject prefabObject = runner.Config.PrefabTable.Load(prefabId, true);
        BreakableObject breakable = prefabObject != null
            ? prefabObject.GetComponent<BreakableObject>()
            : null;
        if (breakable == null)
        {
            error = "The configured prefab has no BreakableObject on its root.";
            return false;
        }

        if (!breakable.PickupPrefab.IsValid)
        {
            error = "The breakable has no valid pickup prefab.";
            return false;
        }

        NetworkPrefabId pickupPrefabId =
            runner.Config.PrefabTable.GetId((NetworkObjectGuid)breakable.PickupPrefab);
        NetworkObject pickupPrefab = pickupPrefabId.IsValid
            ? runner.Config.PrefabTable.Load(pickupPrefabId, true)
            : null;
        NetworkLootPickup pickup = pickupPrefab != null
            ? pickupPrefab.GetComponent<NetworkLootPickup>()
            : null;
        if (pickup == null || pickup.LootCatalog != breakable.LootCatalog)
        {
            error = "The pickup prefab is missing, unregistered, or uses a different loot catalog.";
            return false;
        }

        if (breakable.DropCapacity <= 0 ||
            breakable.LootTable == null ||
            breakable.LootTable.MaximumDistinctStacks > breakable.DropCapacity)
        {
            error = "The breakable table and drop offsets have incompatible capacities.";
            return false;
        }

        return LootContainerContentTableValidation.TryCreateSnapshot(
            breakable.LootTable,
            breakable.LootCatalog,
            breakable.DropCapacity,
            NetworkLootContainer.MaxLootTypes,
            out snapshot,
            out error);
    }

    private static void CompensateFailedSpawn(
        NetworkRunner runner,
        NetworkObject spawnedObject,
        string objectKind,
        ref bool fatalIntegrationFailure)
    {
        if (runner == null || spawnedObject == null || !spawnedObject.Id.IsValid)
        {
            return;
        }

        NetworkId spawnedId = spawnedObject.Id;
        try
        {
            runner.Despawn(spawnedObject);
            if (runner.TryFindObject(spawnedId, out NetworkObject remainingObject) &&
                ReferenceEquals(remainingObject, spawnedObject))
            {
                fatalIntegrationFailure = true;
                Debug.LogError(
                    $"[NetworkSpawnManager] Compensating despawn did not remove {objectKind} object {spawnedId}.",
                    spawnedObject);
            }
        }
        catch (Exception exception)
        {
            fatalIntegrationFailure = true;
            Debug.LogException(exception, spawnedObject);
        }
    }

    public override void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer || runner != _runner)
            return;

        if (_startupContext.IsValid &&
            _startupContext.Mode == SessionStartupMode.HostMigrationResume &&
            (_hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Open ||
             _hostMigrationRecoveryState == HostMigrationRecoveryWindowState.Sealing) &&
            TryResolveHostMigrationProfile(player, out ProfileId migrationProfileId))
        {
            bool terminal = false;
            if (_restoredHostMigrationParticipants.TryGetValue(
                    migrationProfileId,
                    out NetworkObject restoredParticipant) &&
                restoredParticipant != null &&
                restoredParticipant.TryGetBehaviour(out NetworkRaidParticipant migrationParticipant))
            {
                var key = new ControlledReturnKey(
                    migrationProfileId.Value,
                    migrationParticipant.RaidGenerationId.ToString());
                terminal |= migrationParticipant.IsReturnAuthorized ||
                            migrationParticipant.State == RaidParticipantState.Extracted ||
                            migrationParticipant.State == RaidParticipantState.Aborted ||
                            _controlledReturns.IsTerminal(in key);
            }

            _earlyHostMigrationReconnects.Remove(migrationProfileId);
            RemoveRecoveredHostMigrationProfile(
                migrationProfileId,
                player,
                requeue: !terminal);
            if (terminal)
            {
                _hostMigrationEligibleProfiles.Remove(migrationProfileId);
                _hostMigrationUnresolvedProfiles.Add(migrationProfileId);
            }

            Debug.Log(
                $"[HM-MULTI] PlayerLeft invalidated recovered profile '{migrationProfileId.Value}' during {_hostMigrationRecoveryState}.",
                this);
            return;
        }

        if (!_spawnedPlayers.TryGetValue(player, out NetworkObject participantObject) ||
            participantObject == null ||
            !participantObject.TryGetBehaviour(out NetworkRaidParticipant participant))
        {
            RemovePlayerRouting(player);
            Debug.LogWarning(
                $"[NetworkSpawnManager] Player {player} left without an authoritative participant mapping. " +
                "The departure was not classified as a Controlled Return.",
                this);
            return;
        }

        string profileId = participant.ProfileId.ToString();
        string generationId = participant.RaidGenerationId.ToString();
        var departureKey = new ControlledReturnKey(profileId, generationId);
        bool controlledReturn = participant.State == RaidParticipantState.Defeated &&
            _controlledReturns.TryConsume(in departureKey);

        _spawnedPlayers.Remove(player);
        _spawnedAvatars.Remove(player, out NetworkObject avatarObject);
        RemovePlayerRouting(player);

        if (participant.State == RaidParticipantState.Defeated)
        {
            if (controlledReturn)
            {
                _controlledReturns.MarkTerminal(in departureKey);
                Debug.Log(
                    $"[RAID-SPECTATOR] Controlled Return consumed for ProfileId={profileId}, " +
                    $"RaidGenerationId={generationId}. Participant and corpse remain authoritative.",
                    participant);
            }
            else
            {
                Debug.LogWarning(
                    $"[RAID-SPECTATOR] Defeated participant '{profileId}' disconnected without a " +
                    "Controlled Return authorization. Preserving state for future recovery policy.",
                    participant);
            }

            return;
        }

        _controlledReturns.MarkTerminal(in departureKey);
        participant.TryAbortForClosure();
        if (avatarObject != null)
        {
            runner.Despawn(avatarObject);
        }

        runner.Despawn(participantObject);

        Debug.Log($"Despawned participant for player {player}.");
    }

    private bool TryResolveHostMigrationProfile(
        PlayerRef player,
        out ProfileId profileId)
    {
        foreach (KeyValuePair<ProfileId, PlayerRef> pair in _hostMigrationRecoveredProfiles)
        {
            if (pair.Value == player)
            {
                profileId = pair.Key;
                return true;
            }
        }

        foreach (KeyValuePair<ProfileId, PlayerRef> pair in _earlyHostMigrationReconnects)
        {
            if (pair.Value == player)
            {
                profileId = pair.Key;
                return true;
            }
        }

        profileId = default;
        return false;
    }

    private void RemovePlayerRouting(PlayerRef player)
    {
        _admittedPlayers.Remove(player);
        _admissionData.Remove(player);

        string profileToRemove = null;
        foreach (KeyValuePair<string, PlayerRef> pair in _admittedProfiles)
        {
            if (pair.Value == player)
            {
                profileToRemove = pair.Key;
                break;
            }
        }

        if (profileToRemove != null)
        {
            _admittedProfiles.Remove(profileToRemove);
        }
    }
    public override void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (runner == _runner)
        {
            if (_hostMigrationCompletion != null &&
                !_hostMigrationCompletion.Task.IsCompleted)
            {
                CompleteHostMigration(
                    HostMigrationCompletionStatus.Failure,
                    $"Replacement runner shut down before migration completion. Reason={shutdownReason}.");
            }
            _admittedPlayers.Clear();
            _spawnedPlayers.Clear();
            _spawnedAvatars.Clear();
            _controlledReturns.Clear();
            _admittedProfiles.Clear();
            _spawnedEnemies.Clear();
            _lootSpawnState.Clear();
            _breakableSpawnState.Clear();
            _lootSessionSeed = 0;
            _hasLootSessionSeed = false;
            _spawnPointLookup.Clear();
            _matchController = null;
            _runner = null;
            _sceneSpawnPointConfiguration = null;
            _currentSceneLoadGeneration = 0;
            _lastCompletedSceneLoadGeneration = -1;
            _sceneLoadState = SceneLoadProcessingState.None;
            _sceneSpawnStatus = SceneSpawnConfigurationStatus.None;
            _spawnsBlocked = true;

            _resumedScenePipelineReady = false;
            _snapshotRestoreReported = false;
            _snapshotRestoreSucceeded = false;
            ClearPendingHostMigrationRebindData();
            _hostMigrationCompletion = null;
            ClearHostMigrationRoster();
            Debug.Log("[NetworkSpawnManager] Shutdown complete. Cleared all states and references.");
        }
    }

    private bool CanUseCurrentSceneSpawnPoints(NetworkRunner runner)
    {
        if (runner != _runner)
            return false;

        if (_spawnsBlocked)
            return false;

        if (_sceneLoadState != SceneLoadProcessingState.Completed)
            return false;

        if (_sceneSpawnStatus != SceneSpawnConfigurationStatus.SpawnPointsReady)
            return false;

        if (_matchController == null || _matchController.Runner != runner)
            return false;

        NetworkMatchController.MatchPhase phase = _matchController.Phase;
        bool phaseAllowsSpawning = phase == NetworkMatchController.MatchPhase.WaitingForPlayers ||
                                   phase == NetworkMatchController.MatchPhase.Starting ||
                                   phase == NetworkMatchController.MatchPhase.InProgress;
        if (!phaseAllowsSpawning)
            return false;

        if (_sceneSpawnPointConfiguration == null)
            return false;

        return true;
    }

    /// <summary>
    /// Validates spawning that does not consume the current scene spawn-point configuration.
    /// </summary>
    private bool CanSpawnAtExplicitTransform(NetworkRunner runner)
    {
        if (runner == null || runner != _runner)
            return false;

        if (!runner.IsServer)
            return false;

        if (_matchController == null)
            return false;

        NetworkMatchController.MatchPhase phase = _matchController.Phase;
        return phase == NetworkMatchController.MatchPhase.WaitingForPlayers ||
               phase == NetworkMatchController.MatchPhase.Starting ||
               phase == NetworkMatchController.MatchPhase.InProgress;
    }

    public NetworkObject Spawn(
        NetworkPrefabRef prefab,
        SpawnGroupType group)
    {
        return Spawn(prefab, group, UnityEngine.Random.Range(0, int.MaxValue));
    }

    public NetworkObject Spawn(
        NetworkPrefabRef prefab,
        SpawnGroupType group,
        int spawnSeed)
    {
        if (_runner == null)
        {
            Debug.LogError("NetworkRunner not initialized.");
            return null;
        }

        if (!_runner.IsServer)
        {
            Debug.LogWarning("Only the server can spawn NetworkObjects.");
            return null;
        }

        if (group == SpawnGroupType.Loot ||
            group == SpawnGroupType.Breakables ||
            prefab == _lootContainerPrefab ||
            prefab == _breakablePrefab)
        {
            Debug.LogError("[NetworkSpawnManager] Randomized scene entities must use their dedicated initial spawn pipeline.", this);
            return null;
        }

        if (!CanUseCurrentSceneSpawnPoints(_runner))
        {
            Debug.LogError("[NetworkSpawnManager] Spawn failed: Scene spawn points are unavailable or blocked.");
            return null;
        }

        GetSpawnTransform(
            group,
            spawnSeed,
            out Vector3 position,
            out Quaternion rotation);

        return _runner.Spawn(
            prefab,
            position,
            rotation);
    }

    public NetworkObject Spawn(
        NetworkPrefabRef prefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (prefab == _lootContainerPrefab || prefab == _breakablePrefab)
        {
            Debug.LogError("[NetworkSpawnManager] Randomized scene entities cannot bypass their dedicated initial-content pipeline.", this);
            return null;
        }

        if (!CanSpawnAtExplicitTransform(_runner))
        {
            Debug.LogError("[NetworkSpawnManager] Spawn failed: Runner is not initialized or lacks authority.");
            return null;
        }

        return _runner.Spawn(
            prefab,
            position,
            rotation);
    }

    private void GetSpawnTransform(
        SpawnGroupType group,
        int seed,
        out Vector3 position,
        out Quaternion rotation)
    {
        if (!_spawnPointLookup.TryGetValue(group, out Transform[] spawnPoints))
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return;
        }

        int spawnIndex = Mathf.Abs(seed) % spawnPoints.Length;

        Transform spawnPoint = spawnPoints[spawnIndex];

        position = spawnPoint.position;
        rotation = spawnPoint.rotation;
    }

    private bool TryGetSpawnTransformByIndex(
        SpawnGroupType group,
        int index,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
        if (!_spawnPointLookup.TryGetValue(group, out Transform[] spawnPoints) ||
            spawnPoints == null || index < 0 || index >= spawnPoints.Length || spawnPoints[index] == null)
        {
            return false;
        }

        position = spawnPoints[index].position;
        rotation = spawnPoints[index].rotation;
        return true;
    }

    private bool TryValidatePlayerSpawnPreflight(out string failure)
    {
        failure = null;
        if (_launchContext == null ||
            !_spawnPointLookup.TryGetValue(SpawnGroupType.Players, out Transform[] spawnPoints) ||
            spawnPoints == null)
        {
            failure = "Canonical launch context or Players spawn group is missing.";
            return false;
        }

        return RaidParticipantSpawnRules.ValidateSpawnPoints(
            spawnPoints,
            _launchContext.ParticipantProfileIds.Count,
            out failure);
    }
}

public enum HostBootstrapResult
{
    BootstrapCompleted,
    HostAdmittedSpawnPending,
    AdmissionFailed,
    InvalidRunner,
    NoAuthority,
    InvalidCoordinator
}
