using Fusion;
using System;
using System.Threading.Tasks;
using UnityEngine;

public sealed class FusionSessionLauncher : MonoBehaviour
{
    [Header("Session")]
    [SerializeField]
    private string _sessionName;

    [SerializeField, Min(1)]
    private int _maxPlayers = 4;

    [Header("Spawning Configuration")]
    [SerializeField]
    private PlayerClassCatalog _playerClassCatalog;

    [SerializeField]
    private NetworkPrefabRef[] _enemyPrefabs;

    [Header("Coordinator Configuration")]
    [SerializeField]
    private NetworkPrefabRef _matchControllerPrefab;

    private NetworkRunner _runner;
    private GameObject _runnerObject;
    private NetworkSpawnManager _spawnManager;
    private NetworkMatchController _matchController;
    private ExtractionSanctuaryAssignmentService _sanctuaryAssignmentService;
    private bool _isStarting;

    public NetworkRunner Runner => _runner;
    public NetworkMatchController MatchController => _matchController;

    public async Task<bool> StartSessionAsync(string sessionName, GameMode mode, PlayerClassId selectedClass)
    {
        if (string.IsNullOrEmpty(sessionName) || string.IsNullOrWhiteSpace(sessionName))
            throw new Exception("Invalid session code. The code cannot be empty or null.");

        // Acepta exclusivamente Host y Client
        if (mode != GameMode.Host && mode != GameMode.Client)
        {
            throw new ArgumentException($"Unsupported game mode: {mode}. Only GameMode.Host and GameMode.Client are supported.");
        }

        // Valida el prefab del coordinador en modo Host
        if (mode == GameMode.Host && !_matchControllerPrefab.IsValid)
        {
            throw new InvalidOperationException("[FusionSessionLauncher] Match coordinator prefab is invalid or missing.");
        }

        var joinData = new PlayerJoinData(selectedClass);
        if (!PlayerJoinDataCodec.TryEncode(joinData, out byte[] token))
        {
            throw new ArgumentException($"Invalid or unsupported selected class: {selectedClass}");
        }

        if (_isStarting || _runner != null)
            return false;

        _isStarting = true;

        try
        {
            _runnerObject = new GameObject("NetworkRunner");
            _runner = _runnerObject.AddComponent<NetworkRunner>();
            _runnerObject.AddComponent<EntityRegistry>();
            _runnerObject.AddComponent<InMemoryPlayerStashService>();
            _sanctuaryAssignmentService = _runnerObject.AddComponent<ExtractionSanctuaryAssignmentService>();
            if (!_sanctuaryAssignmentService.Initialize(_runner, mode))
            {
                Debug.LogError(
                    "[FusionSessionLauncher] Failed to initialize the sanctuary assignment service for the requested session.",
                    this);
                await ShutdownAndDestroyRunnerAsync();
                return false;
            }

            _runnerObject.AddComponent<LocalInputContext>();

            // 1. Create and associate the NetworkSpawnManager with the runner before callbacks/StartGame
            _spawnManager = _runnerObject.AddComponent<NetworkSpawnManager>();
            if (!_spawnManager.InitializeForRunner(_runner, _playerClassCatalog, _enemyPrefabs))
            {
                Debug.LogError("[FusionSessionLauncher] Failed to initialize NetworkSpawnManager for the current runner.");
                await ShutdownAndDestroyRunnerAsync();
                return false;
            }

            // 2. Discover and register callbacks automatically.
            // Since NetworkSpawnManager is a component of the runner GameObject, Fusion discovers it automatically.
            // We do NOT need to call _runner.AddCallbacks(_spawnManager) manually to avoid double-registration,
            // but if we want to be explicit without double-registering, we can rely on auto-discovery.
            // However, to satisfy "Como el manager existe en el objeto del runner antes de StartGame, preferir el descubrimiento automático de Fusion",
            // we do NOT add it manually here.

            LocalPlayerJoinContext joinContext = _runnerObject.GetComponent<LocalPlayerJoinContext>();
            if (joinContext == null)
            {
                joinContext = _runnerObject.AddComponent<LocalPlayerJoinContext>();
            }
            joinContext.Initialize(in joinData);

            DontDestroyOnLoad(_runnerObject);
            _runner.ProvideInput = true;

            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                PlayerCount = _maxPlayers,
                ConnectionToken = token
            };

            if (mode == GameMode.Client)
            {
                args.EnableClientSessionCreation = false;
            }
            else if (mode == GameMode.Host)
            {
                // Create host session initially closed and hidden to prevent race conditions
                args.IsOpen = false;
                args.IsVisible = false;
            }

            StartGameResult result = await _runner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogError(
                    $"Fusion failed to start. Reason: {result.ShutdownReason}",
                    this);

                await ShutdownAndDestroyRunnerAsync();
                return false;
            }

            Debug.Log(
                $"Fusion session started. " +
                $"Session: {_runner.SessionInfo.Name}. " +
                $"Peer: {(_runner.IsServer ? "Host" : "Client")}.",
                this);

            if (_runner.IsServer)
            {
                NetworkObject coordObj = _runner.Spawn(_matchControllerPrefab, flags: NetworkSpawnFlags.DontDestroyOnLoad);
                if (coordObj == null)
                {
                    Debug.LogError("[FusionSessionLauncher] Host bootstrap failed: Could not spawn match coordinator prefab.");
                    await ShutdownAndDestroyRunnerAsync();
                    return false;
                }

                _matchController = coordObj.GetComponent<NetworkMatchController>();
                if (_matchController == null)
                {
                    Debug.LogError("[FusionSessionLauncher] Host bootstrap failed: NetworkMatchController component not found on spawned prefab.");
                    await ShutdownAndDestroyRunnerAsync();
                    return false;
                }

                // 2. Bind coordinator to NetworkSpawnManager
                if (!_spawnManager.BindMatchController(_matchController))
                {
                    Debug.LogError("[FusionSessionLauncher] Host bootstrap failed: Could not bind coordinator to spawn manager.");
                    await ShutdownAndDestroyRunnerAsync();
                    return false;
                }

                // 3. Atomically perform Host player bootstrap (admission + spawn attempt)
                HostBootstrapResult bootstrapResult = _spawnManager.TryBootstrapHost(_runner, _matchController);
                if (bootstrapResult != HostBootstrapResult.BootstrapCompleted && bootstrapResult != HostBootstrapResult.HostAdmittedSpawnPending)
                {
                    Debug.LogError($"[FusionSessionLauncher] Host bootstrap failed: {bootstrapResult}.");
                    await ShutdownAndDestroyRunnerAsync();
                    return false;
                }

                // 4. Open and show the session after successful coordinator and Host initialization
                _runner.SessionInfo.IsOpen = true;
                _runner.SessionInfo.IsVisible = true;

                Debug.Log($"[FusionSessionLauncher] Host bootstrap completed ({bootstrapResult}). Session is now open and visible.");
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            await ShutdownAndDestroyRunnerAsync();
            throw;
        }
        finally
        {
            _isStarting = false;
        }
    }

    private async Task ShutdownAndDestroyRunnerAsync()
    {
        // Capture local references before clearing them
        NetworkRunner runnerToShutdown = _runner;
        GameObject objToDestroy = _runnerObject;

        _runner = null;
        _runnerObject = null;
        _spawnManager = null;
        _matchController = null;
        _sanctuaryAssignmentService = null;

        if (runnerToShutdown != null && runnerToShutdown.IsRunning)
        {
            try
            {
                await runnerToShutdown.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FusionSessionLauncher] Exception during runner shutdown: {ex.Message}");
            }
        }

        if (objToDestroy != null)
        {
            Destroy(objToDestroy);
        }
    }
}
