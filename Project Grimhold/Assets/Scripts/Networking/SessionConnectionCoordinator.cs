using System;
using System.Collections.Generic;
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
    private float _clientJoinRetryDelaySeconds = 0.5f;

    [SerializeField, Min(0f)]
    private float _raidClosureHostGraceSeconds = 5f;

    [Header("Direct Development Manifest")]
    [SerializeField]
    private string _developmentRaidId = "development-raid";

    [SerializeField]
    private string _developmentAccessSecret = "development-secret";

    [SerializeField]
    private string _developmentHostProfileId;

    [SerializeField]
    private string[] _developmentAdmittedProfileIds;

    [SerializeField, Min(1)]
    private int _developmentLaunchSequence = 1;

    private readonly SessionConnectionStateMachine _stateMachine = new();
    private bool _operationActive;
    private bool _isQuitting;
    private PlayerClassId _selectedBuild = PlayerClassId.None;
    private RaidTransitionTicket? _activeTicket;
    private int _acknowledgedLaunchSequence;
    private bool _launchDispatchActive;
    private bool _raidAdmissionConfirmed;
    private bool _loadoutConfirmationPending;
    private bool _raidClosureReturnStarted;
    private float _raidClosureHostShutdownAt = -1f;
    private SessionTransitionResult? _pendingTransitionFailure;

    private const int MaximumHostCodeCreationAttempts = 5;

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

        ApplicationStashContext stashContext = FindAnyObjectByType<ApplicationStashContext>();
        if (stashContext == null || stashContext.LoadoutService == null)
        {
            Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] Cannot reserve loadout without the application stash context.", this);
            return false;
        }

        string reservationId = Guid.NewGuid().ToString("N");
        StashOperationResult reservationResult = stashContext.LoadoutService.TryCreateLoadoutReservation(
            localProfile,
            reservationId,
            out System.Collections.Generic.IReadOnlyList<StashItem> reservedItems);
        if (reservationResult != StashOperationResult.Success)
        {
            Debug.LogWarning($"[{nameof(SessionConnectionCoordinator)}] Loadout reservation failed: {reservationResult}.", this);
            return false;
        }

        var reservation = new PendingLoadoutReservation(reservationId, reservedItems);

        RaidConnectionRole role = manifest.HostProfileId == localProfile
            ? RaidConnectionRole.Host
            : RaidConnectionRole.Client;
        var request = new RaidConnectionRequest(manifest.RaidId, role, manifest.SessionName);
        _activeTicket = new RaidTransitionTicket(
            request,
            manifest,
            reservation,
            _selectedBuild,
            SessionConnectionState.Town);
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
        if (!TryRollbackActiveReservation())
        {
            Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] Could not roll back the cancelled raid reservation.", this);
            return;
        }

        _activeTicket = null;
    }

    /// <summary>
    /// Leaves Town and creates a code-admitted raid as Host. The session remains open until
    /// authoritative raid closure so clients can join later with the same code.
    /// </summary>
    public Task<SessionTransitionResult> CreateCodeRaidAsync()
    {
        return StartCodeRaidAsync(null, RaidConnectionRole.Host);
    }

    /// <summary>
    /// Leaves Town and joins the existing raid identified by the supplied six-digit code.
    /// A missing or invalid session is a definitive failure and recovers the player to Town.
    /// </summary>
    public Task<SessionTransitionResult> JoinCodeRaidAsync(string code)
    {
        return StartCodeRaidAsync(code, RaidConnectionRole.Client);
    }

    /// <summary>Consumes the latest player-facing transition failure once.</summary>
    public bool TryConsumeLastTransitionFailure(out SessionTransitionResult result)
    {
        if (!_pendingTransitionFailure.HasValue)
        {
            result = default;
            return false;
        }

        result = _pendingTransitionFailure.Value;
        _pendingTransitionFailure = null;
        return true;
    }

    /// <summary>
    /// Determines whether a Client should keep polling while a frozen cohort waits for its Host.
    /// Manual code joins never poll because their Host must create the session first.
    /// </summary>
    public static bool ShouldRetryRaidSessionAvailability(
        in RaidLaunchManifest manifest,
        RaidConnectionRole role,
        ShutdownReason shutdownReason)
    {
        return manifest.IsValid && !manifest.AllowsCodeAdmission &&
               role == RaidConnectionRole.Client &&
               FusionSessionLauncher.IsSessionAvailabilityPending(shutdownReason);
    }

    private async Task<SessionTransitionResult> StartCodeRaidAsync(string code, RaidConnectionRole role)
    {
        if (_operationActive)
        {
            return SessionTransitionResult.Busy;
        }

        bool isHostCreation = role == RaidConnectionRole.Host;
        if (!isHostCreation && !RaidCode.TryParse(code, out _))
        {
            return SessionTransitionResult.InvalidRequest;
        }

        if (State != SessionConnectionState.Town || _hubLauncher.Runner == null ||
            !PlayerJoinDataCodec.IsSupported(_selectedBuild) || !IsSceneEnabled(_gameplaySceneName))
        {
            return SessionTransitionResult.InvalidRequest;
        }

        ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        ApplicationStashContext stashContext = FindAnyObjectByType<ApplicationStashContext>();
        if (stashContext == null || stashContext.LoadoutService == null)
        {
            return SessionTransitionResult.LoadoutReservationFailed;
        }

        string reservationId = Guid.NewGuid().ToString("N");
        StashOperationResult reservationResult = stashContext.LoadoutService.TryCreateLoadoutReservation(
            localProfile,
            reservationId,
            out IReadOnlyList<StashItem> reservedItems);
        if (reservationResult != StashOperationResult.Success)
        {
            return SessionTransitionResult.LoadoutReservationFailed;
        }

        var reservation = new PendingLoadoutReservation(reservationId, reservedItems);
        SessionTransitionResult result = SessionTransitionResult.ConnectionFailed;
        int attemptCount = isHostCreation ? MaximumHostCodeCreationAttempts : 1;
        for (int attempt = 0; attempt < attemptCount; attempt++)
        {
            RaidCode raidCode;
            if (isHostCreation)
            {
                raidCode = GenerateCandidateRaidCode();
            }
            else if (!RaidCode.TryParse(code, out raidCode))
            {
                result = SessionTransitionResult.InvalidRequest;
                break;
            }
            RaidLaunchManifest manifest = RaidLaunchManifest.Code.CreateManifest(raidCode.Value);
            var request = new RaidConnectionRequest(raidCode, role);
            var ticket = new RaidTransitionTicket(
                request,
                manifest,
                reservation,
                _selectedBuild,
                SessionConnectionState.Town);
            _activeTicket = ticket;
            result = await EnterRaidAsync(ticket, isHostCreation && attempt < attemptCount - 1);

            if (result == SessionTransitionResult.Succeeded ||
                !isHostCreation ||
                _raidLauncher.LastStartShutdownReason != ShutdownReason.GameIdAlreadyExists)
            {
                break;
            }
        }
        if ((result == SessionTransitionResult.InvalidRequest ||
             result == SessionTransitionResult.InvalidState ||
             result == SessionTransitionResult.Busy) &&
            _activeTicket.HasValue)
        {
            if (!TryRollbackActiveReservation())
            {
                return SessionTransitionResult.LoadoutRollbackFailed;
            }

            _activeTicket = null;
        }

        if (result != SessionTransitionResult.Succeeded && State != SessionConnectionState.Town)
        {
            result = await RecoverTownAfterRaidFailureAsync(result);
        }

        return result;
    }

    private static RaidCode GenerateCandidateRaidCode()
    {
        while (true)
        {
            string candidate = UnityEngine.Random.Range(0, 1_000_000).ToString("D6");
            if (RaidCode.TryParse(candidate, out RaidCode code))
            {
                return code;
            }
        }
    }

    private void Update()
    {
        ObserveCompletedRaid();
        if (_loadoutConfirmationPending)
        {
            TryConfirmActiveReservation();
        }

        if (_acknowledgedLaunchSequence == 0 || _operationActive)
        {
            return;
        }

        int launchSequence = _acknowledgedLaunchSequence;
        _acknowledgedLaunchSequence = 0;
        _ = BeginAcknowledgedRaidLaunchAsync(launchSequence);
    }

    private void ObserveCompletedRaid()
    {
        if (_raidClosureReturnStarted || _operationActive || State != SessionConnectionState.Raid ||
            _raidLauncher == null || _raidLauncher.Runner == null || _raidLauncher.MatchController == null ||
            _raidLauncher.MatchController.Phase != NetworkMatchController.MatchPhase.Finished)
        {
            return;
        }

        if (_raidLauncher.Runner.IsServer)
        {
            if (_raidClosureHostShutdownAt < 0f)
            {
                _raidClosureHostShutdownAt = Time.unscaledTime + _raidClosureHostGraceSeconds;
                return;
            }

            if (Time.unscaledTime < _raidClosureHostShutdownAt)
            {
                return;
            }
        }

        _raidClosureReturnStarted = true;
        _ = ReturnToTownAsync();
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

            if (this == null || !_activeTicket.HasValue ||
                _activeTicket.Value.Manifest.LaunchSequence != launchSequence)
            {
                return;
            }

            SessionTransitionResult result = await EnterRaidAsync(ticket);
            if (result != SessionTransitionResult.Succeeded)
            {
                Debug.LogError(
                    $"[{nameof(SessionConnectionCoordinator)}] Coordinated raid launch failed. " +
                    $"Role={ticket.Request.Role}; Session={ticket.Request.SessionName}; Result={result}.",
                    this);
            }
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
                if (!TryRollbackActiveReservation())
                {
                    TransitionTo(SessionConnectionState.Failed);
                    return SessionTransitionResult.LoadoutRollbackFailed;
                }
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
            if (_raidAdmissionConfirmed && _loadoutConfirmationPending && !TryConfirmActiveReservation())
            {
                Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] Admission completed but the local reservation confirmation is still pending.", this);
                return SessionTransitionResult.LoadoutConfirmationFailed;
            }

            if (!_raidAdmissionConfirmed && !TryRollbackActiveReservation())
            {
                Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] Raid failed before admission but reservation rollback failed.", this);
                return SessionTransitionResult.LoadoutRollbackFailed;
            }

            _activeTicket = null;
            _raidAdmissionConfirmed = false;
            _loadoutConfirmationPending = false;
            return SessionTransitionResult.Succeeded;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (!_raidAdmissionConfirmed && !TryRollbackActiveReservation())
            {
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.LoadoutRollbackFailed;
            }
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
        return await EnterRaidAsync(ticket, true);
    }

    private async Task<SessionTransitionResult> EnterRaidAsync(
        RaidTransitionTicket ticket,
        bool recoverTownOnFailure)
    {
        if (_operationActive)
        {
            return SessionTransitionResult.Busy;
        }

        ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        RaidConnectionRole expectedRole = ticket.HasManifest && !ticket.Manifest.AllowsCodeAdmission
            ? (ticket.Manifest.HostProfileId == localProfile ? RaidConnectionRole.Host : RaidConnectionRole.Client)
            : ticket.Request.Role;
        if (!ticket.IsValid ||
            (ticket.HasManifest && !ticket.Manifest.Contains(localProfile)) ||
            (ticket.HasManifest && ticket.Request.Role != expectedRole) ||
            !PlayerJoinDataCodec.IsSupported(ticket.SelectedBuild) ||
            !IsSceneEnabled(_gameplaySceneName))
        {
            return SessionTransitionResult.InvalidRequest;
        }

        bool continuationAttempt = State == SessionConnectionState.ConnectingRaid && _hubLauncher.Runner == null;
        if ((!continuationAttempt && State != SessionConnectionState.Town) ||
            (!continuationAttempt && _hubLauncher.Runner == null))
        {
            return SessionTransitionResult.InvalidState;
        }

        _operationActive = true;
        _selectedBuild = ticket.SelectedBuild;
        _activeTicket = ticket.WithState(SessionConnectionState.PreparingRaid);

        try
        {
            if (!continuationAttempt)
            {
                TransitionTo(SessionConnectionState.PreparingRaid);
            }

            if (!continuationAttempt && !await _hubLauncher.ShutdownAndDestroyRunnerAsync())
            {
                    return recoverTownOnFailure
                        ? await RecoverTownAfterRaidFailureAsync(SessionTransitionResult.ShutdownFailed)
                        : SessionTransitionResult.ShutdownFailed;
            }

            if (State != SessionConnectionState.ConnectingRaid)
            {
                TransitionTo(SessionConnectionState.ConnectingRaid);
            }
            UpdateTicketState(SessionConnectionState.ConnectingRaid);

            GameMode mode = ticket.Request.Role == RaidConnectionRole.Host
                ? GameMode.Host
                : GameMode.Client;

            bool started = false;
            bool availabilityWaitLogged = false;
            while (!started)
            {
                started = await _raidLauncher.StartCoordinatedSessionAsync(
                    ticket.Request.SessionName,
                    mode,
                    _selectedBuild,
                    _gameplaySceneName,
                    ticket.HasManifest ? ticket.Manifest : default,
                    ticket.LoadoutReservation);
                if (started)
                {
                    break;
                }

                bool hostSessionPending = ticket.HasManifest &&
                                          ShouldRetryRaidSessionAvailability(
                                              ticket.Manifest,
                                              ticket.Request.Role,
                                              _raidLauncher.LastStartShutdownReason);
                if (!hostSessionPending || _isQuitting)
                {
                    break;
                }

                if (!availabilityWaitLogged)
                {
                    availabilityWaitLogged = true;
                    Debug.Log(
                        $"[{nameof(SessionConnectionCoordinator)}] Waiting for Host raid session " +
                        $"'{ticket.Request.SessionName}' to become available.",
                        this);
                }

                int retryDelayMilliseconds = Mathf.CeilToInt(_clientJoinRetryDelaySeconds * 1000f);
                if (retryDelayMilliseconds > 0)
                {
                    await Task.Delay(retryDelayMilliseconds);
                }
            }

            if (!started)
            {
                if (_isQuitting)
                {
                    return SessionTransitionResult.ConnectionFailed;
                }

                return recoverTownOnFailure
                    ? await RecoverTownAfterRaidFailureAsync(SessionTransitionResult.ConnectionFailed)
                    : await CleanupFailedRaidAttemptAsync(SessionTransitionResult.ConnectionFailed);
            }

            TransitionTo(SessionConnectionState.Raid);
            UpdateTicketState(SessionConnectionState.Raid);
            _raidAdmissionConfirmed = ticket.HasLoadoutReservation;
            _loadoutConfirmationPending = ticket.HasLoadoutReservation;
            TryConfirmActiveReservation();
            _raidClosureReturnStarted = false;
            _raidClosureHostShutdownAt = -1f;
            _pendingTransitionFailure = null;
            return SessionTransitionResult.Succeeded;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return recoverTownOnFailure
                ? await RecoverTownAfterRaidFailureAsync(SessionTransitionResult.ConnectionFailed)
                : await CleanupFailedRaidAttemptAsync(SessionTransitionResult.ConnectionFailed);
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
            if (_loadoutConfirmationPending && !TryConfirmActiveReservation())
            {
                return SessionTransitionResult.LoadoutConfirmationFailed;
            }

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
            if (_raidAdmissionConfirmed && _loadoutConfirmationPending && !TryConfirmActiveReservation())
            {
                return SessionTransitionResult.LoadoutConfirmationFailed;
            }

            if (!_raidAdmissionConfirmed && !TryRollbackActiveReservation())
            {
                return SessionTransitionResult.LoadoutRollbackFailed;
            }

            _activeTicket = null;
            _raidAdmissionConfirmed = false;
            _loadoutConfirmationPending = false;
            _raidClosureReturnStarted = false;
            _raidClosureHostShutdownAt = -1f;
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
            ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
            if (!TryBuildDevelopmentManifest(sessionName, localProfile, mode, out RaidLaunchManifest manifest))
            {
                return SessionTransitionResult.InvalidRequest;
            }

            ApplicationStashContext stashContext = FindAnyObjectByType<ApplicationStashContext>();
            if (stashContext == null || stashContext.LoadoutService == null)
            {
                return SessionTransitionResult.LoadoutReservationFailed;
            }

            string reservationId = Guid.NewGuid().ToString("N");
            if (stashContext.LoadoutService.TryCreateLoadoutReservation(
                    localProfile,
                    reservationId,
                    out IReadOnlyList<StashItem> reservedItems) != StashOperationResult.Success)
            {
                return SessionTransitionResult.LoadoutReservationFailed;
            }

            var reservation = new PendingLoadoutReservation(reservationId, reservedItems);
            RaidConnectionRole role = mode == GameMode.Host ? RaidConnectionRole.Host : RaidConnectionRole.Client;
            var request = new RaidConnectionRequest(manifest.RaidId, role, manifest.SessionName);
            _activeTicket = new RaidTransitionTicket(request, manifest, reservation, selectedBuild, SessionConnectionState.ConnectingRaid);
            TransitionTo(SessionConnectionState.ConnectingRaid);
            if (!await ShutdownActiveRunnersAsync())
            {
                if (!TryRollbackActiveReservation())
                {
                    TransitionTo(SessionConnectionState.Failed);
                    return SessionTransitionResult.LoadoutRollbackFailed;
                }
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.ShutdownFailed;
            }

            bool started = await _raidLauncher.StartCoordinatedSessionAsync(
                sessionName,
                mode,
                selectedBuild,
                _gameplaySceneName,
                manifest,
                reservation);
            if (!started)
            {
                if (!TryRollbackActiveReservation())
                {
                    TransitionTo(SessionConnectionState.Failed);
                    return SessionTransitionResult.LoadoutRollbackFailed;
                }
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.ConnectionFailed;
            }

            TransitionTo(SessionConnectionState.Raid);
            _raidAdmissionConfirmed = true;
            _loadoutConfirmationPending = true;
            TryConfirmActiveReservation();
            return SessionTransitionResult.Succeeded;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (!_raidAdmissionConfirmed && !TryRollbackActiveReservation())
            {
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.LoadoutRollbackFailed;
            }
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
        if (_raidAdmissionConfirmed && _loadoutConfirmationPending && !TryConfirmActiveReservation())
        {
            Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] Admission completed but the local reservation confirmation is still pending.", this);
            return SessionTransitionResult.LoadoutConfirmationFailed;
        }

        if (!_raidAdmissionConfirmed && !TryRollbackActiveReservation())
        {
            Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] Raid failed before admission but reservation rollback failed.", this);
            return SessionTransitionResult.LoadoutRollbackFailed;
        }

        _activeTicket = null;
        _raidAdmissionConfirmed = false;
        _loadoutConfirmationPending = false;
        _pendingTransitionFailure = originalFailure;
        return originalFailure;
    }

    private async Task<SessionTransitionResult> CleanupFailedRaidAttemptAsync(
        SessionTransitionResult failure)
    {
        if (_raidLauncher != null && _raidLauncher.Runner != null &&
            !await _raidLauncher.ShutdownAndDestroyRunnerAsync())
        {
            return SessionTransitionResult.RecoveryFailed;
        }

        return failure;
    }

    private bool TryRollbackActiveReservation()
    {
        if (!_activeTicket.HasValue || !_activeTicket.Value.HasLoadoutReservation)
        {
            return true;
        }

        ApplicationStashContext stashContext = FindAnyObjectByType<ApplicationStashContext>();
        if (stashContext == null || stashContext.LoadoutService == null)
        {
            return false;
        }

        ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        StashOperationResult result = stashContext.LoadoutService.TryRollbackLoadoutReservation(
            localProfile,
            _activeTicket.Value.LoadoutReservation.ReservationId);
        return result == StashOperationResult.Success;
    }

    private bool TryBuildDevelopmentManifest(
        string sessionName,
        ProfileId localProfile,
        GameMode mode,
        out RaidLaunchManifest manifest)
    {
        manifest = default;
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            return false;
        }

        ProfileId hostProfile;
        try
        {
            hostProfile = string.IsNullOrWhiteSpace(_developmentHostProfileId)
                ? localProfile
                : new ProfileId(_developmentHostProfileId);
        }
        catch (ArgumentException)
        {
            return false;
        }

        var profiles = new List<ProfileId>();
        if (_developmentAdmittedProfileIds != null)
        {
            for (int index = 0; index < _developmentAdmittedProfileIds.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(_developmentAdmittedProfileIds[index]))
                {
                    continue;
                }

                try
                {
                    profiles.Add(new ProfileId(_developmentAdmittedProfileIds[index]));
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
        }

        if (profiles.Count == 0)
        {
            profiles.Add(hostProfile);
        }

        if (!profiles.Contains(localProfile) ||
            (mode == GameMode.Host && hostProfile != localProfile) ||
            (mode == GameMode.Client && hostProfile == localProfile))
        {
            return false;
        }

        manifest = new RaidLaunchManifest(
            string.IsNullOrWhiteSpace(_developmentRaidId) ? $"development-{sessionName}" : _developmentRaidId,
            sessionName,
            string.IsNullOrWhiteSpace(_developmentAccessSecret) ? "development-secret" : _developmentAccessSecret,
            hostProfile,
            profiles,
            Mathf.Max(1, _developmentLaunchSequence));
        return manifest.IsValid;
    }

    private bool TryConfirmActiveReservation()
    {
        if (!_activeTicket.HasValue || !_activeTicket.Value.HasLoadoutReservation)
        {
            _loadoutConfirmationPending = false;
            return true;
        }

        ApplicationStashContext stashContext = FindAnyObjectByType<ApplicationStashContext>();
        if (stashContext == null || stashContext.LoadoutService == null)
        {
            return false;
        }

        ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        StashOperationResult result = stashContext.LoadoutService.TryConfirmLoadoutReservation(
            localProfile,
            _activeTicket.Value.LoadoutReservation.ReservationId);
        if (result == StashOperationResult.Success)
        {
            _loadoutConfirmationPending = false;
            return true;
        }

        return false;
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
            if (_raidAdmissionConfirmed && _loadoutConfirmationPending && !TryConfirmActiveReservation())
            {
                Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] Admission completed during an unexpected shutdown, but confirmation remains pending.", this);
                return;
            }

            if (!_raidAdmissionConfirmed && !TryRollbackActiveReservation())
            {
                Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] Unexpected shutdown occurred before admission and rollback failed.", this);
                return;
            }

            _activeTicket = null;
            _raidAdmissionConfirmed = false;
            _loadoutConfirmationPending = false;
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
