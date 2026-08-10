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

    [Header("Coordinated Raid Transition")]
    [SerializeField, Min(0f)]
    private float _clientLaunchDelaySeconds = 1f;

    [SerializeField, Min(1)]
    private int _clientJoinAttempts = 5;

    [SerializeField, Min(0f)]
    private float _clientJoinRetryDelaySeconds = 0.5f;

    private readonly SessionConnectionStateMachine _stateMachine = new();
    private bool _operationActive;
    private bool _isQuitting;
    private PlayerClassId _selectedBuild = PlayerClassId.None;
    private RaidTransitionTicket? _activeTicket;
    private int _acknowledgedLaunchSequence;
    private bool _launchDispatchActive;

    public SessionConnectionState State => _stateMachine.State;
    public RaidTransitionTicket? ActiveTicket => _activeTicket;
    public bool IsTransitioning => _operationActive;

    public event Action<SessionConnectionState> StateChanged;

    /// <summary>
    /// Stores a directed Town queue manifest before the Town runner is shut down.
    /// The method is idempotent for the same launch sequence.
    /// </summary>
    public bool ReceiveRaidLaunchManifest(in RaidLaunchManifest manifest)
    {
        if (!manifest.IsValid || State != SessionConnectionState.Town || _operationActive)
        {
            return false;
        }

        ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        if (!manifest.Contains(localProfile) || !PlayerJoinDataCodec.IsSupported(_selectedBuild))
        {
            return false;
        }

        if (_activeTicket.HasValue && _activeTicket.Value.HasManifest)
        {
            return _activeTicket.Value.Manifest.LaunchSequence == manifest.LaunchSequence &&
                   string.Equals(_activeTicket.Value.Manifest.RaidId, manifest.RaidId, StringComparison.Ordinal);
        }

        RaidConnectionRole role = manifest.HostProfileId == localProfile
            ? RaidConnectionRole.Host
            : RaidConnectionRole.Client;
        var request = new RaidConnectionRequest(manifest.RaidId, role, manifest.SessionName);
        _activeTicket = new RaidTransitionTicket(request, manifest, _selectedBuild, SessionConnectionState.Town);
        return true;
    }

    /// <summary>
    /// Starts a locally stored manifest exactly once after the queue has received all acknowledgements.
    /// </summary>
    public void BeginAcknowledgedRaidLaunch(int launchSequence)
    {
        if (State != SessionConnectionState.Town || _operationActive || _launchDispatchActive ||
            _acknowledgedLaunchSequence != 0 ||
            !_activeTicket.HasValue || !_activeTicket.Value.HasManifest ||
            _activeTicket.Value.Manifest.LaunchSequence != launchSequence)
        {
            return;
        }

        // Update dispatches this on the following rendered frame so the Town runner is
        // never shut down from inside Fusion's RPC invocation stack.
        _launchDispatchActive = true;
        _acknowledgedLaunchSequence = launchSequence;
    }

    /// <summary>
    /// Discards a locally stored ticket when the authoritative Town cohort aborts before release.
    /// </summary>
    public void CancelPendingRaidLaunch(int launchSequence)
    {
        if (State != SessionConnectionState.Town || _operationActive ||
            !_activeTicket.HasValue || !_activeTicket.Value.HasManifest ||
            _activeTicket.Value.Manifest.LaunchSequence != launchSequence)
        {
            return;
        }

        _acknowledgedLaunchSequence = 0;
        _launchDispatchActive = false;
        _activeTicket = null;
    }

    private void Update()
    {
        if (_acknowledgedLaunchSequence == 0 || _operationActive)
        {
            return;
        }

        int launchSequence = _acknowledgedLaunchSequence;
        _acknowledgedLaunchSequence = 0;
        _ = BeginAcknowledgedRaidLaunchAsync(launchSequence);
    }

    private async Task BeginAcknowledgedRaidLaunchAsync(int launchSequence)
    {
        try
        {
            if (!_activeTicket.HasValue || !_activeTicket.Value.HasManifest ||
                _activeTicket.Value.Manifest.LaunchSequence != launchSequence)
            {
                return;
            }

            RaidTransitionTicket ticket = _activeTicket.Value;
            if (ticket.Request.Role == RaidConnectionRole.Client && _clientLaunchDelaySeconds > 0f)
            {
                int delayMilliseconds = Mathf.CeilToInt(_clientLaunchDelaySeconds * 1000f);
                await Task.Delay(delayMilliseconds);
            }

            if (this == null || !_activeTicket.HasValue ||
                _activeTicket.Value.Manifest.LaunchSequence != launchSequence)
            {
                return;
            }

            await EnterRaidAsync(ticket);
        }
        finally
        {
            if (this != null)
            {
                _launchDispatchActive = false;
            }
        }
    }

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
        var ticket = new RaidTransitionTicket(request, _selectedBuild, SessionConnectionState.PreparingRaid);
        return await EnterRaidAsync(ticket);
    }

    /// <summary>
    /// Replaces the active Town runner using a manifest previously acknowledged by the Town queue.
    /// </summary>
    public async Task<SessionTransitionResult> EnterRaidAsync(RaidTransitionTicket ticket)
    {
        if (_operationActive)
        {
            return SessionTransitionResult.Busy;
        }

        ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        RaidConnectionRole expectedRole = ticket.HasManifest && ticket.Manifest.HostProfileId == localProfile
            ? RaidConnectionRole.Host
            : RaidConnectionRole.Client;
        if (!ticket.IsValid ||
            (ticket.HasManifest && !ticket.Manifest.Contains(localProfile)) ||
            (ticket.HasManifest && ticket.Request.Role != expectedRole) ||
            !PlayerJoinDataCodec.IsSupported(ticket.SelectedBuild) ||
            !IsSceneEnabled(_gameplaySceneName))
        {
            return SessionTransitionResult.InvalidRequest;
        }

        if (State != SessionConnectionState.Town || _hubLauncher.Runner == null)
        {
            return SessionTransitionResult.InvalidState;
        }

        _operationActive = true;
        _selectedBuild = ticket.SelectedBuild;
        _activeTicket = ticket.WithState(SessionConnectionState.PreparingRaid);

        try
        {
            TransitionTo(SessionConnectionState.PreparingRaid);

            if (!await _hubLauncher.ShutdownAndDestroyRunnerAsync())
            {
                return await RecoverTownAfterRaidFailureAsync(SessionTransitionResult.ShutdownFailed);
            }

            TransitionTo(SessionConnectionState.ConnectingRaid);
            UpdateTicketState(SessionConnectionState.ConnectingRaid);

            GameMode mode = ticket.Request.Role == RaidConnectionRole.Host
                ? GameMode.Host
                : GameMode.Client;

            bool started = false;
            int attempts = ticket.HasManifest && ticket.Request.Role == RaidConnectionRole.Client
                ? Mathf.Max(1, _clientJoinAttempts)
                : 1;
            for (int attempt = 0; attempt < attempts && !started; attempt++)
            {
                started = await _raidLauncher.StartCoordinatedSessionAsync(
                    ticket.Request.SessionName,
                    mode,
                    _selectedBuild,
                    _gameplaySceneName,
                    ticket.HasManifest ? ticket.Manifest : default);
                if (!started && attempt + 1 < attempts)
                {
                    int retryDelayMilliseconds = Mathf.CeilToInt(_clientJoinRetryDelaySeconds * 1000f);
                    if (retryDelayMilliseconds > 0)
                    {
                        await Task.Delay(retryDelayMilliseconds);
                    }
                }
            }

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
