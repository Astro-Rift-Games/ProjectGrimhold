using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the application's local Fusion connection lifecycle across MainMenu, Town and raid.
/// It serializes transitions and guarantees that a replacement runner is not created until
/// the previous runner composition has completed shutdown and destruction.
/// </summary>
[DisallowMultipleComponent]
public sealed class SessionConnectionCoordinator : MonoBehaviour
{
    public static SessionConnectionCoordinator Instance { get; private set; }

    [Header("Launchers")]
    [SerializeField]
    private HubSessionLauncher _hubLauncher;

    [SerializeField]
    private FusionSessionLauncher _raidLauncher;

    [Header("Network Scenes")]
    [SerializeField]
    private string _townSceneName = "Lobby-Town";

    [SerializeField]
    private string _gameplaySceneName = "Gameplay";

    private readonly SessionConnectionStateMachine _stateMachine = new();
    private bool _operationActive;
    private bool _isQuitting;
    private PlayerClassId _selectedBuild = PlayerClassId.None;
    private RaidTransitionTicket? _activeTicket;

    public SessionConnectionState State => _stateMachine.State;
    public RaidTransitionTicket? ActiveTicket => _activeTicket;
    public bool IsTransitioning => _operationActive;

    public event Action<SessionConnectionState> StateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        DontDestroyOnLoad(gameObject);

        if (_hubLauncher == null)
        {
            _hubLauncher = GetComponent<HubSessionLauncher>();
        }

        if (_raidLauncher == null)
        {
            _raidLauncher = GetComponent<FusionSessionLauncher>();
        }

        if (_hubLauncher == null || _raidLauncher == null)
        {
            Debug.LogError($"{nameof(SessionConnectionCoordinator)} requires both session launchers.", this);
            TransitionTo(SessionConnectionState.Failed);
            enabled = false;
            return;
        }

        _hubLauncher.RunnerShutdownObserved += OnHubRunnerShutdown;
        _raidLauncher.RunnerShutdownObserved += OnRaidRunnerShutdown;
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    private void OnDestroy()
    {
        if (_hubLauncher != null)
        {
            _hubLauncher.RunnerShutdownObserved -= OnHubRunnerShutdown;
        }

        if (_raidLauncher != null)
        {
            _raidLauncher.RunnerShutdownObserved -= OnRaidRunnerShutdown;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Connects the local profile to the Shared Mode Town from MainMenu or a failed state.
    /// </summary>
    public async Task<SessionTransitionResult> ConnectToTownAsync(PlayerClassId selectedBuild)
    {
        if (_operationActive)
        {
            return SessionTransitionResult.Busy;
        }

        if (!PlayerJoinDataCodec.IsSupported(selectedBuild) || !IsSceneEnabled(_townSceneName))
        {
            return SessionTransitionResult.InvalidRequest;
        }

        if (State == SessionConnectionState.Town && _hubLauncher.Runner != null)
        {
            return SessionTransitionResult.Succeeded;
        }

        if (State != SessionConnectionState.MainMenu && State != SessionConnectionState.Failed)
        {
            return SessionTransitionResult.InvalidState;
        }

        _operationActive = true;
        _selectedBuild = selectedBuild;
        try
        {
            if (!TransitionTo(SessionConnectionState.ConnectingTown))
            {
                return SessionTransitionResult.InvalidState;
            }

            if (!await ShutdownActiveRunnersAsync())
            {
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.ShutdownFailed;
            }

            bool started = await _hubLauncher.StartHubSessionAsync(selectedBuild, _townSceneName);
            if (!started)
            {
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.ConnectionFailed;
            }

            TransitionTo(SessionConnectionState.Town);
            _activeTicket = null;
            return SessionTransitionResult.Succeeded;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            TransitionTo(SessionConnectionState.Failed);
            return SessionTransitionResult.ConnectionFailed;
        }
        finally
        {
            _operationActive = false;
        }
    }

    /// <summary>
    /// Replaces the active Shared runner with a new Host or Client raid runner.
    /// </summary>
    public async Task<SessionTransitionResult> EnterRaidAsync(RaidConnectionRequest request)
    {
        if (_operationActive)
        {
            return SessionTransitionResult.Busy;
        }

        if (!request.IsValid ||
            !PlayerJoinDataCodec.IsSupported(_selectedBuild) ||
            !IsSceneEnabled(_gameplaySceneName))
        {
            return SessionTransitionResult.InvalidRequest;
        }

        if (State != SessionConnectionState.Town || _hubLauncher.Runner == null)
        {
            return SessionTransitionResult.InvalidState;
        }

        _operationActive = true;
        _activeTicket = new RaidTransitionTicket(
            request,
            _selectedBuild,
            SessionConnectionState.PreparingRaid);

        try
        {
            TransitionTo(SessionConnectionState.PreparingRaid);

            if (!await _hubLauncher.ShutdownAndDestroyRunnerAsync())
            {
                return await RecoverTownAfterRaidFailureAsync(SessionTransitionResult.ShutdownFailed);
            }

            TransitionTo(SessionConnectionState.ConnectingRaid);
            UpdateTicketState(SessionConnectionState.ConnectingRaid);

            GameMode mode = request.Role == RaidConnectionRole.Host
                ? GameMode.Host
                : GameMode.Client;

            bool started = await _raidLauncher.StartCoordinatedSessionAsync(
                request.SessionName,
                mode,
                _selectedBuild,
                _gameplaySceneName);

            if (!started)
            {
                return await RecoverTownAfterRaidFailureAsync(SessionTransitionResult.ConnectionFailed);
            }

            TransitionTo(SessionConnectionState.Raid);
            UpdateTicketState(SessionConnectionState.Raid);
            return SessionTransitionResult.Succeeded;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return await RecoverTownAfterRaidFailureAsync(SessionTransitionResult.ConnectionFailed);
        }
        finally
        {
            _operationActive = false;
        }
    }

    /// <summary>
    /// Replaces the current raid runner with a fresh Shared Mode Town runner.
    /// It also provides the explicit recovery entry point from a failed transition.
    /// </summary>
    public async Task<SessionTransitionResult> ReturnToTownAsync()
    {
        if (_operationActive)
        {
            return SessionTransitionResult.Busy;
        }

        if (!PlayerJoinDataCodec.IsSupported(_selectedBuild) || !IsSceneEnabled(_townSceneName))
        {
            return SessionTransitionResult.InvalidRequest;
        }

        if (State != SessionConnectionState.Raid && State != SessionConnectionState.Failed)
        {
            return SessionTransitionResult.InvalidState;
        }

        _operationActive = true;
        try
        {
            TransitionTo(SessionConnectionState.ReturningTown);
            UpdateTicketState(SessionConnectionState.ReturningTown);

            if (!await ShutdownActiveRunnersAsync())
            {
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.ShutdownFailed;
            }

            bool started = await _hubLauncher.StartHubSessionAsync(_selectedBuild, _townSceneName);
            if (!started)
            {
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.RecoveryFailed;
            }

            TransitionTo(SessionConnectionState.Town);
            _activeTicket = null;
            return SessionTransitionResult.Succeeded;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            TransitionTo(SessionConnectionState.Failed);
            return SessionTransitionResult.RecoveryFailed;
        }
        finally
        {
            _operationActive = false;
        }
    }

    /// <summary>
    /// Preserves the existing MainMenu raid lobby as an explicit development-only route.
    /// The coordinator still owns the runner so the single-runner invariant is maintained.
    /// </summary>
    public async Task<SessionTransitionResult> StartDirectRaidForDevelopmentAsync(
        string sessionName,
        GameMode mode,
        PlayerClassId selectedBuild)
    {
        if (_operationActive)
        {
            return SessionTransitionResult.Busy;
        }

        if ((mode != GameMode.Host && mode != GameMode.Client) ||
            string.IsNullOrWhiteSpace(sessionName) ||
            !PlayerJoinDataCodec.IsSupported(selectedBuild))
        {
            return SessionTransitionResult.InvalidRequest;
        }

        if (State != SessionConnectionState.MainMenu && State != SessionConnectionState.Failed)
        {
            return SessionTransitionResult.InvalidState;
        }

        _operationActive = true;
        _selectedBuild = selectedBuild;
        try
        {
            TransitionTo(SessionConnectionState.ConnectingRaid);
            if (!await ShutdownActiveRunnersAsync())
            {
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.ShutdownFailed;
            }

            bool started = await _raidLauncher.StartSessionAsync(sessionName, mode, selectedBuild);
            if (!started)
            {
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.ConnectionFailed;
            }

            TransitionTo(SessionConnectionState.Raid);
            return SessionTransitionResult.Succeeded;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            TransitionTo(SessionConnectionState.Failed);
            return SessionTransitionResult.ConnectionFailed;
        }
        finally
        {
            _operationActive = false;
        }
    }

    private async Task<SessionTransitionResult> RecoverTownAfterRaidFailureAsync(
        SessionTransitionResult originalFailure)
    {
        TransitionTo(SessionConnectionState.ReturningTown);
        UpdateTicketState(SessionConnectionState.ReturningTown);

        if (!await ShutdownActiveRunnersAsync())
        {
            TransitionTo(SessionConnectionState.Failed);
            return SessionTransitionResult.RecoveryFailed;
        }

        bool recovered = await _hubLauncher.StartHubSessionAsync(_selectedBuild, _townSceneName);
        if (!recovered)
        {
            TransitionTo(SessionConnectionState.Failed);
            return SessionTransitionResult.RecoveryFailed;
        }

        TransitionTo(SessionConnectionState.Town);
        _activeTicket = null;
        return originalFailure;
    }

    private async Task<bool> ShutdownActiveRunnersAsync()
    {
        bool hubShutdown = await _hubLauncher.ShutdownAndDestroyRunnerAsync();
        bool raidShutdown = await _raidLauncher.ShutdownAndDestroyRunnerAsync();
        return hubShutdown && raidShutdown;
    }

    private void OnHubRunnerShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (_operationActive || _isQuitting || State != SessionConnectionState.Town)
        {
            return;
        }

        RecoverFromUnexpectedShutdown();
    }

    private void OnRaidRunnerShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (_operationActive || _isQuitting || State != SessionConnectionState.Raid)
        {
            return;
        }

        RecoverFromUnexpectedShutdown();
    }

    private async void RecoverFromUnexpectedShutdown()
    {
        if (_operationActive || !PlayerJoinDataCodec.IsSupported(_selectedBuild))
        {
            TransitionTo(SessionConnectionState.Failed);
            return;
        }

        _operationActive = true;
        try
        {
            TransitionTo(SessionConnectionState.ReturningTown);
            UpdateTicketState(SessionConnectionState.ReturningTown);
            if (!await ShutdownActiveRunnersAsync() ||
                !await _hubLauncher.StartHubSessionAsync(_selectedBuild, _townSceneName))
            {
                TransitionTo(SessionConnectionState.Failed);
                return;
            }

            TransitionTo(SessionConnectionState.Town);
            _activeTicket = null;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            TransitionTo(SessionConnectionState.Failed);
        }
        finally
        {
            _operationActive = false;
        }
    }

    private bool TransitionTo(SessionConnectionState nextState)
    {
        if (!_stateMachine.TryTransition(nextState))
        {
            Debug.LogError($"Invalid session transition: {State} -> {nextState}.", this);
            return false;
        }

        StateChanged?.Invoke(nextState);
        return true;
    }

    private void UpdateTicketState(SessionConnectionState state)
    {
        if (_activeTicket.HasValue)
        {
            _activeTicket = _activeTicket.Value.WithState(state);
        }
    }

    private static bool IsSceneEnabled(string sceneName)
    {
        return NetworkSceneBuildIndexResolver.Resolve(sceneName) >= 0;
    }
}
