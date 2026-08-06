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

    public Task<bool> StartSessionAsync(string sessionName, GameMode mode, PlayerClassId selectedClass)
    {
        return StartSessionInternalAsync(sessionName, mode, selectedClass, SessionStartupContext.FreshSession);
    }

    private async Task<bool> StartSessionInternalAsync(string sessionName, GameMode mode, PlayerClassId selectedClass, SessionStartupContext startupContext)
    {
        if (!startupContext.IsValid)
            throw new ArgumentException("Invalid startup context provided to session launcher.");

        if (string.IsNullOrEmpty(sessionName) || string.IsNullOrWhiteSpace(sessionName))
            throw new Exception("Invalid session code. The code cannot be empty or null.");

        // Acepta exclusivamente Host y Client
        if (mode != GameMode.Host && mode != GameMode.Client)
        {
            throw new ArgumentException($"Unsupported game mode: {mode}. Only GameMode.Host and GameMode.Client are supported.");
        }

        // Valida el prefab del coordinador en modo Host, sólo si se requiere bootstrap del Host
        if (mode == GameMode.Host && startupContext.ShouldExecuteHostBootstrap && !_matchControllerPrefab.IsValid)
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
            if (!NetworkRunnerFactory.TryCreate(
                mode,
                startupContext,
                _playerClassCatalog,
                _enemyPrefabs,
                in joinData,
                token,
                out var composition))
            {
                Debug.LogError("[FusionSessionLauncher] Failed to create runner composition via factory.", this);
                return false;
            }

            _runnerObject = composition.RunnerObject;
            _runner = composition.Runner;
            _spawnManager = composition.SpawnManager;
            _sanctuaryAssignmentService = composition.SanctuaryAssignmentService;

            var listener = _runnerObject.AddComponent<LauncherShutdownListener>();
            listener.Initialize(this, _runner);

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
                if (startupContext.ShouldExecuteHostBootstrap)
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
                else
                {
                    // Host Migration Resume
                    Debug.Log("[FusionSessionLauncher] Host Migration Resume: skipping fresh host bootstrap (coordinator creation & opening session handled by restore).");
                }
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
        if (_runner != null)
        {
            if (_runner.IsRunning)
            {
                await _runner.Shutdown();
            }
            if (_runnerObject != null)
            {
                Destroy(_runnerObject);
            }
        }

        // Manual cleanup if something failed before runner got properly initialized or shut down gracefully
        ClearReferencesOnShutdown(_runner);
    }

    public void ClearReferencesOnShutdown(NetworkRunner shutdownRunner)
    {
        if (_runner != shutdownRunner)
        {
            return;
        }

        _runner = null;
        _runnerObject = null;
        _spawnManager = null;
        _matchController = null;
        _sanctuaryAssignmentService = null;
    }
}
