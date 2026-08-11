using Fusion;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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
    private readonly HashSet<string> _departedProfiles = new();
    private readonly Dictionary<string, PlayerRef> _admittedProfiles = new();
    private readonly Dictionary<PlayerRef, NetworkObject> _pendingHostMigrationReconnects = new();
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
    private RaidLaunchManifest _raidManifest;

    private int _currentSceneLoadGeneration = 0;
    private int _lastCompletedSceneLoadGeneration = -1;
    private SceneLoadProcessingState _sceneLoadState = SceneLoadProcessingState.None;
    private SceneSpawnConfigurationStatus _sceneSpawnStatus = SceneSpawnConfigurationStatus.None;
    private bool _spawnsBlocked = true;
    private InitialRaidBootstrapState _initialRaidBootstrapState = InitialRaidBootstrapState.NotStarted;

    private bool _resumedScenePipelineReady;
    private bool _snapshotRestoreReported;
    private bool _snapshotRestoreSucceeded;

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
        _departedProfiles.Clear();
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
        _raidManifest = default;
        _pendingHostMigrationReconnects.Clear();
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
        RaidLaunchManifest raidManifest)
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
        _raidManifest = raidManifest;

        _admittedPlayers.Clear();
        _spawnedPlayers.Clear();
        _spawnedAvatars.Clear();
        _admissionData.Clear();
        _departedProfiles.Clear();
        _admittedProfiles.Clear();
        _pendingHostMigrationReconnects.Clear();
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
    }

    public void ReportSnapshotRestoreResult(
        bool success,
        IReadOnlyDictionary<PlayerRef, NetworkObject> restoredPlayerObjects = null,
        IReadOnlyDictionary<PlayerRef, NetworkObject> pendingReconnects = null)
    {
        if (_snapshotRestoreReported)
        {
            Debug.LogWarning("[NetworkSpawnManager] Snapshot restore result already reported. Ignoring duplicate.");
            return;
        }

        _snapshotRestoreReported = true;
        _snapshotRestoreSucceeded = success;
        TryAdvanceHostMigrationRestoreState(restoredPlayerObjects, pendingReconnects);
    }

    private void TryAdvanceHostMigrationRestoreState(
        IReadOnlyDictionary<PlayerRef, NetworkObject> restoredPlayerObjects = null,
        IReadOnlyDictionary<PlayerRef, NetworkObject> pendingReconnects = null)
    {
        if (_sceneLoadState == SceneLoadProcessingState.Failed || _sceneLoadState == SceneLoadProcessingState.HostMigrationRestoreFailed)
            return;

        if (!_resumedScenePipelineReady || !_snapshotRestoreReported)
        {
            _sceneLoadState = SceneLoadProcessingState.AwaitingHostMigrationRestore;
            _spawnsBlocked = true;
            return;
        }

        if (_snapshotRestoreSucceeded)
        {
            if (restoredPlayerObjects != null && restoredPlayerObjects.Count > 0)
            {
                foreach (var kvp in restoredPlayerObjects)
                {
                    _admittedPlayers.Add(kvp.Key);
                    _spawnedPlayers[kvp.Key] = kvp.Value;
                    if (kvp.Value != null &&
                        kvp.Value.TryGetBehaviour(out NetworkRaidParticipant participant) &&
                        participant.TryResolveCurrentAvatar(out NetworkObject avatar))
                    {
                        _spawnedAvatars[kvp.Key] = avatar;
                    }
                }
            }
            else
            {
                Debug.LogWarning("[NetworkSpawnManager] Snapshot restore succeeded but no player mapping was provided or it was empty. No players were repopulated.");
            }

            if (pendingReconnects != null && pendingReconnects.Count > 0)
            {
                foreach (var kvp in pendingReconnects)
                {
                    _pendingHostMigrationReconnects[kvp.Key] = kvp.Value;
                }
                Debug.Log($"[NetworkSpawnManager] Tracking {pendingReconnects.Count} pending reconnects from Host Migration.");
            }

            _sceneLoadState = SceneLoadProcessingState.Completed;
            _spawnsBlocked = false;
            Debug.Log("[NetworkSpawnManager] Host Migration Restore succeeded. Players repopulated and spawns unblocked.");
        }
        else
        {
            _sceneLoadState = SceneLoadProcessingState.HostMigrationRestoreFailed;
            _spawnsBlocked = true;
            Debug.LogError("[NetworkSpawnManager] Host Migration Restore failed.");
        }
    }

    public override void OnConnectRequest(
        NetworkRunner runner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
        if (!runner.IsServer || runner != _runner)
            return;

        bool isHostMigrationWindow = false;
        if (_startupContext.IsValid && _startupContext.Mode == SessionStartupMode.HostMigrationResume)
        {
            if (_matchController != null && _matchController.Phase == NetworkMatchController.MatchPhase.InProgress)
            {
                isHostMigrationWindow = true;
            }
        }

        if (_raidManifest.IsValid && !TryValidateRaidAdmissionToken(token, out _))
        {
            Debug.LogWarning("[NetworkSpawnManager] Refusing connection request with an invalid raid admission token.");
            request.Refuse();
            return;
        }

        bool codeAdmissionWindow = _matchController != null &&
                                   NetworkMatchController.IsCodeAdmissionOpen(
                                       _raidManifest.AllowsCodeAdmission,
                                       _matchController.Phase);
        bool allowJoin = (_matchController != null &&
                          _matchController.Phase == NetworkMatchController.MatchPhase.WaitingForPlayers) ||
                         codeAdmissionWindow ||
                         isHostMigrationWindow;

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
            Debug.Log($"[HM-DIAG] OnPlayerJoined during HostMigrationResume: PlayerRef={player}");
        }

        if (!runner.IsServer || runner != _runner)
        {
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

        if (_matchController == null)
            return false;

        if (!TryGetJoinData(runner, player, out PlayerJoinData joinData))
        {
            return false;
        }

        RaidAdmissionData admission = default;
        bool hasAdmission = _raidManifest.IsValid;
        if (hasAdmission && !TryGetAdmissionData(runner, player, out admission))
        {
            return false;
        }

        if (_departedProfiles.Contains(joinData.ProfileId.Value.ToString()))
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

        if (_pendingHostMigrationReconnects.TryGetValue(player, out NetworkObject pendingObj))
        {
            _pendingHostMigrationReconnects.Remove(player);
            _admittedPlayers.Add(player);
            _spawnedPlayers[player] = pendingObj;

            pendingObj.AssignInputAuthority(player);
            runner.SetPlayerObject(player, pendingObj);

            Debug.Log($"[NetworkSpawnManager] Player {player} readmitted from pending Host Migration reconnect. Reassigned authority to NetworkId {pendingObj.Id}.");
            return true;
        }

        bool admissionPhaseIsOpen = _matchController.Phase == NetworkMatchController.MatchPhase.WaitingForPlayers ||
                                    NetworkMatchController.IsCodeAdmissionOpen(
                                        _raidManifest.AllowsCodeAdmission,
                                        _matchController.Phase);
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

    public void NotifyPendingReconnectCharacterDefeated(NetworkObject obj)
    {
        if (obj == null) return;
        
        PlayerRef? keyToRemove = null;
        foreach (var kvp in _pendingHostMigrationReconnects)
        {
            if (kvp.Value == obj)
            {
                keyToRemove = kvp.Key;
                break;
            }
        }

        if (keyToRemove.HasValue)
        {
            _pendingHostMigrationReconnects.Remove(keyToRemove.Value);
            Debug.Log($"[NetworkSpawnManager] Pending reconnect character for PlayerRef {keyToRemove.Value} was defeated. Removed from pending list.");
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

        if (_raidManifest.IsValid && RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData raidAdmission))
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
            (_raidManifest.IsValid && (!context.HasRaidAdmission || !IsRaidAdmissionValid(context.RaidAdmission))))
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
        _raidManifest.IsValid && !_raidManifest.AllowsCodeAdmission
            ? _raidManifest.AdmittedProfiles.Count
            : 0;
    public int AdmittedRaidProfileCount => _admittedProfiles.Count;

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

    private bool IsRaidAdmissionValid(in RaidAdmissionData admission)
    {
        if (!_raidManifest.IsValid || !admission.IsValid)
        {
            return false;
        }

        if (_raidManifest.RaidCode.IsValid)
        {
            return admission.RaidCode.IsValid &&
                   admission.RaidCode == _raidManifest.RaidCode &&
                   admission.ProfileId.IsValid;
        }

        return string.Equals(admission.RaidId, _raidManifest.RaidId, StringComparison.Ordinal) &&
               string.Equals(admission.AccessSecret, _raidManifest.AccessSecret, StringComparison.Ordinal) &&
               _raidManifest.Contains(admission.ProfileId);
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
            Debug.Log($"[HM-DIAG] SpawnPlayer during HostMigrationResume: PlayerRef={player}");
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
        bool hasAdmission = _raidManifest.IsValid;
        bool loadoutInitialized = !hasAdmission;

        GetSpawnTransform(
            SpawnGroupType.Players,
            player.RawEncoded,
            out Vector3 position,
            out Quaternion rotation);

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

        _admittedPlayers.Remove(player);
        _admissionData.Remove(player);
        if (_admittedProfiles.Count > 0)
        {
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

        if (!_spawnedPlayers.Remove(player, out NetworkObject participantObject))
            return;

        _spawnedAvatars.Remove(player, out NetworkObject avatarObject);
        if (participantObject != null && participantObject.TryGetBehaviour(out NetworkRaidParticipant participant))
        {
            _departedProfiles.Add(participant.ProfileId.ToString());
            participant.TryAbortForClosure();
            if (participant.State != RaidParticipantState.Defeated && avatarObject != null)
            {
                runner.Despawn(avatarObject);
            }
        }
        else if (avatarObject != null)
        {
            runner.Despawn(avatarObject);
        }

        if (participantObject != null)
        {
            runner.Despawn(participantObject);
        }

        Debug.Log($"Despawned participant for player {player}.");
    }

    public override void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (runner == _runner)
        {
            _admittedPlayers.Clear();
            _spawnedPlayers.Clear();
            _spawnedAvatars.Clear();
            _departedProfiles.Clear();
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
            _pendingHostMigrationReconnects.Clear();
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
