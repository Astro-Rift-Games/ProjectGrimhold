using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [SerializeField, Min(1f)]
    private float _clientSessionAvailabilityTimeoutSeconds = 40f;

    [Header("Direct Development Context")]
    [SerializeField]
    private string _developmentRaidCode = "900001";

    [SerializeField]
    private string _developmentHostProfileId;

    [SerializeField]
    private string[] _developmentAdmittedProfileIds;

    [SerializeField, Min(0)]
    [Tooltip("Groups admitted development profiles into teams of size N. 0 keeps all participants in Team 1.")]
    private int _developmentTeamSize;

    [SerializeField, Min(1)]
    private int _developmentLaunchSequence = 1;

    [SerializeField]
    private bool _developmentFriendlyFireEnabled;

    [SerializeField]
    [Tooltip("Editor only: treats the local player as awaiting a partner still in the Dungeon so the Town Ready lock can be validated with one client.")]
    private bool _simulatePartyMemberInDungeon;

    private readonly SessionConnectionStateMachine _stateMachine = new();
    private bool _operationActive;
    private bool _isQuitting;
    private RaidTransitionTicket? _activeTicket;
    private RaidReconnectTicket? _activeReconnectTicket;
    private RaidCode _currentRaidCode;
    private ProfileId _currentProfileId;
    private string _currentRaidGenerationId;
    private string _currentSessionName;
    private RaidConnectionRole _currentRole;
    private TownPartyContinuationContext _pendingPartyContinuation;
    private int _acknowledgedLaunchSequence;
    private bool _launchDispatchActive;
    private bool _raidAdmissionConfirmed;
    private bool _loadoutConfirmationPending;
    private string _reservationParticipationRaidId;
    private NetworkRunner _hostMigrationSourceRunner;
    private Task _participationOutcomeTask = Task.CompletedTask;
    private readonly RaidLossNoticeSlot _lossNotice = new();
    private bool _hostResultsReturnRequested;
    private bool _hostResultsReturnStarted;
    private SessionTransitionResult? _pendingTransitionFailure;
    private ExpeditionPreparationResult? _pendingLaunchRejection;
    private CancellationTokenSource _activeTransitionCancellation;
    private ApplicationStashContext _recoveryStashContext;
    private Task<(bool success, Grimhold.Backend.BackendError error)> _launchReservationSave;

    public SessionConnectionState State => _stateMachine.State;
    public RaidTransitionTicket? ActiveTicket => _activeTicket;
    public bool HasActiveReconnectTicket => ActiveReconnectTicket.HasValue && ActiveReconnectTicket.Value.IsValid;
    public RaidReconnectTicket? ActiveReconnectTicket
    {
        get
        {
            if (!_activeReconnectTicket.HasValue)
            {
                ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
                if (RaidReconnectTicketStore.TryLoad(localProfile, out RaidReconnectTicket persistedTicket))
                {
                    _activeReconnectTicket = persistedTicket;
                }
            }

            return _activeReconnectTicket;
        }
    }
    public TownPartyContinuationContext PendingPartyContinuation => _pendingPartyContinuation;
    public bool HasPendingPartyContinuation => _pendingPartyContinuation != null;

    /// <summary>Whether a Duo partner is still in the active expedition, which blocks a new one from Town.</summary>
    public bool IsAwaitingPartyCompletion =>
        TownPartyRules.IsAwaitingPartyCompletion(_pendingPartyContinuation) || SimulatePartyMemberInDungeon;

#if UNITY_EDITOR
    public bool SimulatePartyMemberInDungeon
    {
        get => _simulatePartyMemberInDungeon;
        set => _simulatePartyMemberInDungeon = value;
    }
#else
    private bool SimulatePartyMemberInDungeon => false;
#endif
    public bool IsTransitioning => _operationActive;

    public event Action<SessionConnectionState> StateChanged;
    public event Action<RaidReconnectTicket?> ReconnectTicketChanged;

    // Preserves the coordinator's existing application-context acquisition boundary.
    private ApplicationStashContext ResolveRecoveryStashContext()
    {
        if (_recoveryStashContext == null)
            _recoveryStashContext = FindAnyObjectByType<ApplicationStashContext>();
        return _recoveryStashContext;
    }


    public ExpeditionPreparationResult ValidateLocalExpeditionEquipment()
    {
        ApplicationStashContext context = ResolveRecoveryStashContext();
        ProfileId profile = LocalProfileProvider.GetOrCreateLocalProfile();
        return context != null && context.ProfileId == profile && context.LoadoutService != null
            ? context.LoadoutService.TryPrepareExpeditionLoadout(profile)
            : ExpeditionPreparationResult.ProfileUnavailable;
    }

    public bool TryGetPendingPartyContinuation(out TownPartyContinuationContext context)
    {
        context = _pendingPartyContinuation;
        return context != null && context.Contains(LocalProfileProvider.GetOrCreateLocalProfile());
    }

    /// <summary>
    /// Preserves one validated Party descriptor across runner replacement. Repeating the same
    /// descriptor is idempotent; a different Party cannot replace an unresolved continuation.
    /// </summary>
    public bool TryPreservePartyContinuation(TownPartyContinuationContext continuation)
    {
        if (continuation == null || !continuation.Contains(LocalProfileProvider.GetOrCreateLocalProfile()))
        {
            return false;
        }

        if (_pendingPartyContinuation == null)
        {
            _pendingPartyContinuation = continuation;
            return true;
        }

        return _pendingPartyContinuation.Equals(continuation);
    }

    public bool ConfirmPartyContinuationRestored(in TownPartySnapshot snapshot)
    {
        if (_pendingPartyContinuation == null ||
            !_pendingPartyContinuation.Matches(snapshot))
        {
            return false;
        }

        _pendingPartyContinuation = null;
        return true;
    }

    public bool AbandonPartyContinuation(TownPartyContinuationContext expectedContext)
    {
        if (_pendingPartyContinuation == null || expectedContext == null ||
            !_pendingPartyContinuation.Equals(expectedContext))
        {
            return false;
        }

        _pendingPartyContinuation = null;
        return true;
    }

    /// <summary>
    /// Stores the canonical context observed from a complete replicated frozen preparation.
    /// The method is idempotent for the same RaidCode and LaunchRevision. A returned
    /// <see cref="RaidLaunchPreparationResult.NotReady"/> is transient and may be retried; every
    /// other failure is permanent for this launch revision and exposes a player-facing reason
    /// through <see cref="TryConsumeLastLaunchRejection"/>.
    /// </summary>
    public RaidLaunchPreparationResult TryStoreRaidLaunchContext(
        RaidLaunchContext launchContext,
        TownPartyContinuationContext partyContinuation)
    {
        if (!isActiveAndEnabled || _isQuitting || launchContext == null || State != SessionConnectionState.Town || _operationActive ||
            !_participationOutcomeTask.IsCompleted)
        {
            return RaidLaunchPreparationResult.NotReady;
        }

        ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        if (launchContext.LocalProfileId != localProfile || partyContinuation == null ||
            !partyContinuation.Contains(localProfile) ||
            !RaidSessionRules.ContainsProfile(launchContext.ParticipantProfileIds, localProfile))
        {
            return RaidLaunchPreparationResult.NotReady;
        }

        if (_activeTicket.HasValue)
        {
            return HasActiveLaunchTicket(
                launchContext.RaidCode.Value,
                launchContext.LaunchRevision,
                localProfile)
                ? ObserveLaunchReservationSave()
                : RaidLaunchPreparationResult.NotReady;
        }

        ApplicationStashContext stashContext = ResolveRecoveryStashContext();
        if (stashContext == null || stashContext.LoadoutService == null)
        {
            Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] Cannot reserve loadout without the application stash context.", this);
            return RejectLaunchPreparation(
                ExpeditionPreparationResult.ProfileUnavailable,
                RaidLaunchPreparationResult.ConfigurationError);
        }

        ExpeditionPreparationResult preparation = stashContext.LoadoutService.TryPrepareExpeditionLoadout(localProfile);
        if (preparation != ExpeditionPreparationResult.Success)
        {
            return RejectLaunchPreparation(
                preparation,
                preparation == ExpeditionPreparationResult.ProfileUnavailable
                    ? RaidLaunchPreparationResult.ConfigurationError
                    : RaidLaunchPreparationResult.Rejected);
        }

        string reservationId = Guid.NewGuid().ToString("N");
        StashOperationResult reservationResult = stashContext.LoadoutService.TryCreateLoadoutReservation(
            localProfile,
            reservationId,
            out PendingLoadoutReservation reservation);
        if (reservationResult != StashOperationResult.Success)
        {
            return RejectLaunchPreparation(
                ExpeditionPreparationResult.ReservationFailed,
                RaidLaunchPreparationResult.Rejected);
        }

        RaidConnectionRole role = launchContext.HostProfileId == localProfile
            ? RaidConnectionRole.Host
            : RaidConnectionRole.Client;
        var request = new RaidConnectionRequest(launchContext.RaidCode, role);
        _activeTicket = new RaidTransitionTicket(
            request,
            reservation,
            SessionConnectionState.Town,
            launchContext);
        if (!TryPreservePartyContinuation(partyContinuation))
        {
            _activeTicket = null;
            stashContext.LoadoutService.TryRollbackLoadoutReservation(localProfile, reservation.ReservationId);
            return RaidLaunchPreparationResult.Rejected;
        }
        var remoteInventoryService = stashContext.GetComponent<RemoteInventoryService>();
        _launchReservationSave = remoteInventoryService != null
            ? remoteInventoryService.SavePendingReservationAsync(reservation)
            : null;
        return ObserveLaunchReservationSave();
    }

    public static ExpeditionPreparationResult ReservationFailureReason(Grimhold.Backend.BackendError error) =>
        error.error == "PREPARED_WEAPON_REQUIRED"
            ? ExpeditionPreparationResult.PreparedWeaponRequired
            : ExpeditionPreparationResult.ReservationFailed;

    private RaidLaunchPreparationResult ObserveLaunchReservationSave()
    {
        if (_launchReservationSave == null) return RaidLaunchPreparationResult.Success;
        if (!_launchReservationSave.IsCompleted) return RaidLaunchPreparationResult.NotReady;
        ExpeditionPreparationResult reason = ExpeditionPreparationResult.ReservationFailed;
        if (_launchReservationSave.Status == TaskStatus.RanToCompletion)
        {
            var response = _launchReservationSave.Result;
            if (response.success) return RaidLaunchPreparationResult.Success;
            reason = ReservationFailureReason(response.error);
        }
        _launchReservationSave = null;
        if (TryRollbackActiveReservation())
        {
            _activeTicket = null;
            _pendingPartyContinuation = null;
        }
        return RejectLaunchPreparation(reason, RaidLaunchPreparationResult.Rejected);
    }

    private RaidLaunchPreparationResult RejectLaunchPreparation(
        ExpeditionPreparationResult reason,
        RaidLaunchPreparationResult result)
    {
        _pendingLaunchRejection = reason;
        return result;
    }

    /// <summary>Consumes the latest player-facing expedition preparation rejection once.</summary>
    public bool TryConsumeLastLaunchRejection(out ExpeditionPreparationResult reason)
    {
        if (!_pendingLaunchRejection.HasValue)
        {
            reason = default;
            return false;
        }

        reason = _pendingLaunchRejection.Value;
        _pendingLaunchRejection = null;
        return true;
    }

    public bool HasActiveLaunchTicket(string raidCode, int launchRevision, ProfileId localProfileId)
    {
        return RaidCode.TryParse(raidCode, out RaidCode parsed) && _activeTicket.HasValue &&
               _activeTicket.Value.LaunchContext != null &&
               _activeTicket.Value.LaunchContext.RaidCode == parsed &&
               _activeTicket.Value.LaunchContext.LaunchRevision == launchRevision &&
               _activeTicket.Value.LaunchContext.LocalProfileId == localProfileId;
    }

    public static bool CanConsumeLaunchRelease(
        in RaidTransitionTicket ticket,
        string raidCode,
        int launchRevision,
        ProfileId localProfileId)
    {
        return RaidCode.TryParse(raidCode, out RaidCode parsed) && ticket.IsValid &&
               ticket.LaunchContext.RaidCode == parsed &&
               ticket.LaunchContext.LaunchRevision == launchRevision &&
               ticket.LaunchContext.LocalProfileId == localProfileId;
    }

    /// <summary>
    /// Starts a locally stored context exactly once after the preparation has released this peer.
    /// </summary>
    public void BeginAcknowledgedRaidLaunch(string raidCode, int launchSequence)
    {
        if (State != SessionConnectionState.Town || _operationActive || _launchDispatchActive ||
            _acknowledgedLaunchSequence != 0 ||
            !_activeTicket.HasValue ||
            !CanConsumeLaunchRelease(
                _activeTicket.Value,
                raidCode,
                launchSequence,
                LocalProfileProvider.GetOrCreateLocalProfile()))
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
    public void CancelPendingRaidLaunch(string raidCode, int launchSequence)
    {
        if (State != SessionConnectionState.Town || _operationActive ||
            !_activeTicket.HasValue ||
            !CanConsumeLaunchRelease(
                _activeTicket.Value,
                raidCode,
                launchSequence,
                LocalProfileProvider.GetOrCreateLocalProfile()))
        {
            return;
        }

        _acknowledgedLaunchSequence = 0;
        _launchDispatchActive = false;
        _launchReservationSave = null;
        if (!TryRollbackActiveReservation())
        {
            Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] Could not roll back the cancelled raid reservation.", this);
            return;
        }

        _activeTicket = null;
        _pendingPartyContinuation = null;
    }

    /// <summary>
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
        RaidLaunchContext launchContext,
        RaidConnectionRole role,
        ShutdownReason shutdownReason)
    {
        return launchContext != null &&
               role == RaidConnectionRole.Client &&
               FusionSessionLauncher.IsSessionAvailabilityPending(shutdownReason);
    }

    /// <summary>
    /// Determines whether a Client should keep polling while a frozen cohort waits for its Host,
    /// bounded by the remaining availability timeout budget.
    /// </summary>
    public static bool ShouldRetryRaidSessionAvailability(
        RaidLaunchContext launchContext,
        RaidConnectionRole role,
        ShutdownReason shutdownReason,
        float remainingSeconds)
    {
        return remainingSeconds > 0f &&
               ShouldRetryRaidSessionAvailability(launchContext, role, shutdownReason);
    }

    internal static bool ShouldRecoverRaidShutdown(
        bool operationActive,
        bool isQuitting,
        SessionConnectionState state,
        ShutdownReason shutdownReason,
        bool isOutgoingHostHandoff = false)
    {
        // Host Migration normally hands the runner to a replacement; only the outgoing Host,
        // which will not be migrated, must return to Town itself.
        return !operationActive && !isQuitting && state == SessionConnectionState.Raid &&
               (shutdownReason != ShutdownReason.HostMigration || isOutgoingHostHandoff);
    }

    private const float HostHandoffRetrySeconds = 3f;
    private bool _hostHandoffInFlight;
    private bool _outgoingHostHandoff;
    private HostHandoffOutcome _lastHostHandoffOutcome = HostHandoffOutcome.Stayed;
    private float _hostHandoffNotBefore;

    private void ObserveHostHandoff()
    {
        if (!HostHandoffPolicy.ShouldAttemptHandoff(
                _hostHandoffInFlight,
                _operationActive,
                State == SessionConnectionState.Raid,
                Time.unscaledTime,
                _hostHandoffNotBefore))
        {
            return;
        }

        NetworkRunner runner = _raidLauncher != null ? _raidLauncher.Runner : null;
        NetworkSpawnManager spawnManager = runner != null
            ? runner.GetComponent<NetworkSpawnManager>()
            : null;
        if (runner == null || !runner.IsRunning || !runner.IsServer || spawnManager == null ||
            !spawnManager.IsLocalHostExitResolved())
        {
            return;
        }

        _hostHandoffInFlight = true;
        _ = AttemptHostHandoffAsync(runner);
    }

    private async Task AttemptHostHandoffAsync(NetworkRunner runner)
    {
        HostHandoffOutcome outcome = HostHandoffOutcome.Stayed;
        try
        {
            outcome = await HostHandoffService.TryHandOffAsync(
                new FusionHostHandoffRunner(runner),
                beforeShutdown: () => _outgoingHostHandoff = true);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        if (this == null)
        {
            return;
        }

        _hostHandoffInFlight = false;
        if (outcome != _lastHostHandoffOutcome)
        {
            // Logged on change only: CloseNormally and Stayed are re-evaluated every retry window.
            _lastHostHandoffOutcome = outcome;
            Debug.Log($"[HM-MULTI] FIX-50 Host handoff attempt outcome={outcome}.", this);
        }

        if (outcome == HostHandoffOutcome.HandedOff)
        {
            return;
        }

        _outgoingHostHandoff = false;
        _hostHandoffNotBefore = Time.unscaledTime + HostHandoffRetrySeconds;
        if (outcome == HostHandoffOutcome.SnapshotRejected)
        {
            Debug.LogWarning(
                $"[{nameof(SessionConnectionCoordinator)}] Host handoff snapshot was rejected; " +
                "the Host keeps the role and retries.",
                this);
        }
    }

    private void Update()
    {
        ObserveTechnicalClosure();
        ObservePendingHostResultsReturn();
        ObserveHostHandoff();
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

    /// <summary>
    /// Registers the operational Host's explicit intent to leave completed Results.
    /// The global transition remains pending while any remote peer belongs to the runner.
    /// </summary>
    public bool RequestHostResultsReturn()
    {
        if (_hostResultsReturnRequested || _hostResultsReturnStarted)
        {
            return true;
        }

        if (!TryResolveHostResultsContext(
                out NetworkRunner runner,
                out NetworkMatchController matchController,
                out NetworkSpawnManager spawnManager,
                out NetworkRaidParticipant participant,
                out bool hasProgressionResultSnapshot))
        {
            return false;
        }

        if (!RaidResultsReturnPolicy.CanRequestHostReturn(
                participant.State,
                participant.FinalizationCause,
                participant.IsExtractionProgressionComplete,
                hasProgressionResultSnapshot,
                runner.IsServer,
                spawnManager.HasRaidingParticipants,
                matchController.Phase == NetworkMatchController.MatchPhase.Finished))
        {
            return false;
        }

        _hostResultsReturnRequested = true;
        return true;
    }

    /// <summary>
    /// FIX-53: a bootstrap or admission-deadline closure aborts the participants without a result, so
    /// there is no Results screen to Return from. The Host leaves through the ordinary Host Return,
    /// which still waits for remote peers and reports the loss in Town.
    /// </summary>
    private float _technicalClosureNotBefore;

    private void ObserveTechnicalClosure()
    {
        // A failed Return resets the request flags; the delay keeps its retry from running every frame.
        if (_hostResultsReturnRequested || _hostResultsReturnStarted || _operationActive ||
            State != SessionConnectionState.Raid || Time.unscaledTime < _technicalClosureNotBefore)
        {
            return;
        }

        NetworkRunner runner = _raidLauncher != null ? _raidLauncher.Runner : null;
        if (runner == null || !runner.IsRunning || !TryResolveMatchController(runner, out NetworkMatchController match) ||
            !RaidLossNoticePolicy.ShouldReturnHostAfterTechnicalClosure(
                runner.IsServer, true, match.Phase, match.ClosureReason))
        {
            return;
        }

        _hostResultsReturnRequested = true;
        _technicalClosureNotBefore = Time.unscaledTime + HostHandoffRetrySeconds;
        Debug.Log(
            $"[FIX-53] Technical raid closure ({match.ClosureReason}, Phase={match.Phase}) routed to Town; " +
            "the Host returns once no remote peer remains.",
            this);
    }

    // Clients only learn the controller through the spawn manager; both read the replicated closure.
    private bool TryResolveMatchController(NetworkRunner runner, out NetworkMatchController match)
    {
        match = _raidLauncher.MatchController;
        if (match == null)
        {
            NetworkSpawnManager spawnManager = runner.GetComponent<NetworkSpawnManager>();
            match = spawnManager != null ? spawnManager.MatchController : null;
        }

        return match != null && match.Object != null && match.Object.IsValid;
    }

    private void ObservePendingHostResultsReturn()
    {
        if (!_hostResultsReturnRequested)
        {
            return;
        }

        NetworkRunner runner = _raidLauncher != null ? _raidLauncher.Runner : null;
        NetworkMatchController matchController = _raidLauncher != null
            ? _raidLauncher.MatchController
            : null;
        NetworkSpawnManager spawnManager = runner != null
            ? runner.GetComponent<NetworkSpawnManager>()
            : null;

        if (runner == null || !runner.IsRunning || !runner.IsServer ||
            matchController == null || spawnManager == null)
        {
            ResetHostResultsReturn();
            return;
        }

        if (!RaidResultsReturnPolicy.ShouldStartHostReturn(
                _hostResultsReturnRequested,
                _hostResultsReturnStarted,
                _operationActive,
                State == SessionConnectionState.Raid,
                hasValidHostRunner: true,
                isMatchFinished: matchController.Phase ==
                    NetworkMatchController.MatchPhase.Finished,
                hasRaidingParticipants: spawnManager.HasRaidingParticipants,
                hasConnectedRemoteParticipants:
                    spawnManager.HasConnectedRemoteParticipants))
        {
            return;
        }

        _hostResultsReturnStarted = true;
        LogHostResultsReturnStarted();
        _ = CompletePendingHostResultsReturnAsync();
    }

    private void LogHostResultsReturnStarted()
    {
        string participantState = "Unresolved";
        if (TryResolveHostResultsContext(
                out _,
                out _,
                out _,
                out NetworkRaidParticipant participant,
                out bool hasProgressionResultSnapshot))
        {
            participantState =
                $"{participant.State}/{participant.FinalizationCause}, " +
                $"HasSnapshot={hasProgressionResultSnapshot}";
        }

        Debug.Log($"[FIX-53] Solo Host Return started. Participant={participantState}.", this);
    }

    private bool TryResolveHostResultsContext(
        out NetworkRunner runner,
        out NetworkMatchController matchController,
        out NetworkSpawnManager spawnManager,
        out NetworkRaidParticipant participant,
        out bool hasProgressionResultSnapshot)
    {
        runner = _raidLauncher != null ? _raidLauncher.Runner : null;
        matchController = _raidLauncher != null ? _raidLauncher.MatchController : null;
        spawnManager = runner != null ? runner.GetComponent<NetworkSpawnManager>() : null;
        participant = null;
        hasProgressionResultSnapshot = false;
        if (State != SessionConnectionState.Raid || _operationActive || runner == null ||
            !runner.IsRunning || !runner.IsServer || matchController == null || spawnManager == null ||
            !runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject participantObject) ||
            participantObject == null ||
            !participantObject.TryGetBehaviour(out participant))
        {
            return false;
        }

        PlayerExpeditionProgressionResolver resolver =
            participant.GetComponent<PlayerExpeditionProgressionResolver>();
        hasProgressionResultSnapshot = resolver != null &&
            resolver.TryGetProgressionResult(out ExpeditionProgressionResult _);
        return true;
    }

    private async Task CompletePendingHostResultsReturnAsync()
    {
        SessionTransitionResult result = await ReturnToTownAsync();
        if (this == null || result == SessionTransitionResult.Succeeded)
        {
            return;
        }

        _hostResultsReturnRequested = false;
        _hostResultsReturnStarted = false;
        _pendingTransitionFailure = result;
    }

    private async Task BeginAcknowledgedRaidLaunchAsync(int launchSequence)
    {
        try
        {
            if (!_activeTicket.HasValue || _activeTicket.Value.LaunchContext == null ||
                _activeTicket.Value.LaunchContext.LaunchRevision != launchSequence)
            {
                return;
            }

            RaidTransitionTicket ticket = _activeTicket.Value;

            if (this == null || !_activeTicket.HasValue ||
                _activeTicket.Value.LaunchContext == null ||
                _activeTicket.Value.LaunchContext.LaunchRevision != launchSequence)
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
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

    private void OnApplicationQuit()
    {
        BeginTermination();
    }

#if UNITY_EDITOR
    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            BeginTermination();
        }
    }
#endif

    private void BeginTermination()
    {
        if (_isQuitting)
        {
            return;
        }

        _isQuitting = true;
        _activeTransitionCancellation?.Cancel();
    }

    private void OnDestroy()
    {
        BeginTermination();
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
        ResetHostResultsReturn();
        _activeTransitionCancellation?.Dispose();
        _activeTransitionCancellation = null;

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
    public async Task<SessionTransitionResult> ConnectToTownAsync()
    {
        if (_isQuitting)
        {
            return SessionTransitionResult.ConnectionFailed;
        }

        if (_operationActive)
        {
            return SessionTransitionResult.Busy;
        }

        if (!IsSceneEnabled(_townSceneName))
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
        CancellationToken cancellationToken = BeginTransitionCancellation();
        try
        {
            if (!TransitionTo(SessionConnectionState.ConnectingTown))
            {
                return SessionTransitionResult.InvalidState;
            }

            bool shutdownSucceeded = await ShutdownActiveRunnersAsync();
            if (_isQuitting)
            {
                return SessionTransitionResult.ConnectionFailed;
            }

            if (!shutdownSucceeded)
            {
                if (!TryRollbackActiveReservation())
                {
                    TransitionTo(SessionConnectionState.Failed);
                    return SessionTransitionResult.LoadoutRollbackFailed;
                }
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.ShutdownFailed;
            }

            bool started = await _hubLauncher.StartHubSessionAsync(_townSceneName, cancellationToken);
            if (_isQuitting)
            {
                return SessionTransitionResult.ConnectionFailed;
            }

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

            CompleteTownEntry();
            return SessionTransitionResult.Succeeded;
        }
        catch (OperationCanceledException) when (_isQuitting)
        {
            return SessionTransitionResult.ConnectionFailed;
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
            EndTransitionCancellation();
            _operationActive = false;
        }
    }

    /// <summary>
    /// Replaces the active Town runner using the canonical context acknowledged by its preparation.
    /// </summary>
    public async Task<SessionTransitionResult> EnterRaidAsync(RaidTransitionTicket ticket)
    {
        return await EnterRaidAsync(ticket, true);
    }

    private async Task<SessionTransitionResult> EnterRaidAsync(
        RaidTransitionTicket ticket,
        bool recoverTownOnFailure)
    {
        if (_isQuitting)
        {
            return SessionTransitionResult.ConnectionFailed;
        }

        if (_operationActive)
        {
            return SessionTransitionResult.Busy;
        }

        ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        if (!ticket.IsValid || ticket.LaunchContext.LocalProfileId != localProfile ||
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
        CancellationToken cancellationToken = BeginTransitionCancellation();
        _activeTicket = ticket.WithState(SessionConnectionState.PreparingRaid);

        try
        {
            if (!continuationAttempt)
            {
                TransitionTo(SessionConnectionState.PreparingRaid);
            }

            if (!continuationAttempt)
            {
                bool shutdownSucceeded = await _hubLauncher.ShutdownAndDestroyRunnerAsync();
                if (_isQuitting)
                {
                    return SessionTransitionResult.ConnectionFailed;
                }

                if (!shutdownSucceeded)
                {
                    return recoverTownOnFailure
                        ? await RecoverTownAfterRaidFailureAsync(SessionTransitionResult.ShutdownFailed)
                        : SessionTransitionResult.ShutdownFailed;
                }
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
            ShutdownReason lastObservedShutdownReason = ShutdownReason.Ok;
            int coordinatedAttempt = 0;
            float deadline = Time.realtimeSinceStartup + _clientSessionAvailabilityTimeoutSeconds;
            while (!started)
            {
                if (_isQuitting)
                {
                    return SessionTransitionResult.ConnectionFailed;
                }

                coordinatedAttempt++;
                started = await _raidLauncher.StartCoordinatedSessionAsync(
                    ticket.Request.SessionName,
                    mode,
                    _gameplaySceneName,
                    ticket.LaunchContext,
                    ticket.LoadoutReservation,
                    cancellationToken);
                if (_isQuitting)
                {
                    return SessionTransitionResult.ConnectionFailed;
                }

                if (started)
                {
                    break;
                }

                ShutdownReason lastReason = _raidLauncher.LastStartShutdownReason;
                float remainingSeconds = deadline - Time.realtimeSinceStartup;
                bool hostSessionPending = ShouldRetryRaidSessionAvailability(
                                              ticket.LaunchContext,
                                              ticket.Request.Role,
                                              lastReason,
                                              remainingSeconds);
                if (!hostSessionPending || _isQuitting)
                {
                    if (ticket.Request.Role == RaidConnectionRole.Client &&
                        ticket.LaunchContext != null &&
                        FusionSessionLauncher.IsSessionAvailabilityPending(lastReason) &&
                        remainingSeconds <= 0f)
                    {
                        Debug.LogWarning(
                            $"[{nameof(SessionConnectionCoordinator)}] Timed out waiting for Host raid session " +
                            $"'{ticket.Request.SessionName}' after {coordinatedAttempt} attempts. LastReason={lastReason}.",
                            this);
                    }
                    break;
                }

                if (!availabilityWaitLogged)
                {
                    availabilityWaitLogged = true;
                    lastObservedShutdownReason = lastReason;
                    Debug.Log(
                        $"[{nameof(SessionConnectionCoordinator)}] Waiting for Host raid session " +
                        $"'{ticket.Request.SessionName}' to become available. Reason={lastReason}.",
                        this);
                }
                else if (lastReason != lastObservedShutdownReason)
                {
                    lastObservedShutdownReason = lastReason;
                    Debug.Log(
                        $"[{nameof(SessionConnectionCoordinator)}] Host raid session availability state changed to {lastReason}.",
                        this);
                }

                float delaySeconds = Mathf.Min(_clientJoinRetryDelaySeconds, remainingSeconds);
                int retryDelayMilliseconds = Mathf.CeilToInt(delaySeconds * 1000f);
                if (retryDelayMilliseconds > 0)
                {
                    await Task.Delay(retryDelayMilliseconds, cancellationToken);
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
            _currentRaidCode = ticket.LaunchContext.RaidCode;
            _currentProfileId = ticket.LaunchContext.LocalProfileId;
            _currentRaidGenerationId = ticket.LaunchContext.RaidCode.RaidId;
            _reservationParticipationRaidId = _currentRaidGenerationId;
            _currentSessionName = ticket.Request.SessionName;
            _currentRole = ticket.Request.Role;

            if (_currentRole == RaidConnectionRole.Client && _raidAdmissionConfirmed &&
                _currentRaidCode.IsValid && _currentProfileId.IsValid &&
                !string.IsNullOrEmpty(_currentRaidGenerationId) && !string.IsNullOrEmpty(_currentSessionName))
            {
                string reservationId = _activeTicket.HasValue && _activeTicket.Value.LoadoutReservation != null
                    ? _activeTicket.Value.LoadoutReservation.ReservationId
                    : string.Empty;
                var clientTicket = new RaidReconnectTicket(
                    _currentRaidCode,
                    _currentProfileId,
                    _currentRaidGenerationId,
                    _currentSessionName,
                    reservationId);
                RaidReconnectTicketStore.Save(clientTicket);
            }

            TryConfirmActiveReservation();
            ResetHostResultsReturn();
            _pendingTransitionFailure = null;
            return SessionTransitionResult.Succeeded;
        }
        catch (OperationCanceledException)
        {
            if (this == null || _isQuitting)
            {
                return SessionTransitionResult.ConnectionFailed;
            }

            return recoverTownOnFailure
                ? await RecoverTownAfterRaidFailureAsync(SessionTransitionResult.ConnectionFailed)
                : await CleanupFailedRaidAttemptAsync(SessionTransitionResult.ConnectionFailed);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (this == null || _isQuitting)
            {
                return SessionTransitionResult.ConnectionFailed;
            }

            return recoverTownOnFailure
                ? await RecoverTownAfterRaidFailureAsync(SessionTransitionResult.ConnectionFailed)
                : await CleanupFailedRaidAttemptAsync(SessionTransitionResult.ConnectionFailed);
        }
        finally
        {
            EndTransitionCancellation();
            _operationActive = false;
        }
    }

    /// <summary>
    /// Attempts to reconnect the local client to the active raid represented by its in-memory ticket.
    /// Shuts down the Town hub runner, connects via StartReconnectClientSessionAsync, and restores
    /// Town if connection fails. Discards ticket permanently upon ConnectionRefused or GameNotFound.
    /// </summary>
    public async Task<SessionTransitionResult> ReconnectToRaidAsync()
    {
        if (_isQuitting)
        {
            return SessionTransitionResult.ConnectionFailed;
        }

        if (_operationActive)
        {
            return SessionTransitionResult.Busy;
        }

        if (!HasActiveReconnectTicket)
        {
            return SessionTransitionResult.InvalidRequest;
        }

        if (State != SessionConnectionState.Town)
        {
            return SessionTransitionResult.InvalidState;
        }

        _operationActive = true;
        CancellationToken cancellationToken = BeginTransitionCancellation();
        RaidReconnectTicket ticket = ActiveReconnectTicket.Value;

        try
        {
            if (!TransitionTo(SessionConnectionState.ConnectingRaid))
            {
                return SessionTransitionResult.InvalidState;
            }

            bool hubShutdown = await _hubLauncher.ShutdownAndDestroyRunnerAsync();
            if (_isQuitting)
            {
                return SessionTransitionResult.ConnectionFailed;
            }

            if (!hubShutdown)
            {
                TransitionTo(SessionConnectionState.Failed);
                return await RecoverTownAfterFailedReconnectAsync();
            }

            bool connected = false;
            bool attemptThrew = false;
            ShutdownReason attemptShutdownReason = ShutdownReason.Error;
            float deadline = Time.realtimeSinceStartup + _clientSessionAvailabilityTimeoutSeconds;
            int attempt = 0;
            while (!connected)
            {
                if (_isQuitting)
                {
                    return SessionTransitionResult.ConnectionFailed;
                }

                attempt++;
                attemptThrew = false;
                attemptShutdownReason = ShutdownReason.Error;
                try
                {
                    connected = await _raidLauncher.StartReconnectClientSessionAsync(ticket, cancellationToken);
                    attemptShutdownReason = _raidLauncher.LastStartShutdownReason;
                }
                catch (OperationCanceledException)
                {
                    attemptThrew = true;
                    if (this == null || _isQuitting)
                    {
                        return SessionTransitionResult.ConnectionFailed;
                    }
                    break;
                }
                catch (Exception ex)
                {
                    attemptThrew = true;
                    Debug.LogException(ex, this);
                    break;
                }

                if (connected)
                {
                    break;
                }

                ShutdownReason lastReason = attemptShutdownReason;
                float remainingSeconds = deadline - Time.realtimeSinceStartup;
                // For reconnect, GameNotFound and GameClosed are immediately terminal because the raid was cancelled or closed (e.g. host continued without teammate).
                // FIX-51: a Host-rejected admission (ConnectionRefused) is terminal too; retrying cannot succeed.
                if (!RaidReconnectPolicy.ShouldRetryAttempt(lastReason, remainingSeconds) || _isQuitting)
                {
                    break;
                }

                float delaySeconds = Mathf.Min(_clientJoinRetryDelaySeconds, remainingSeconds);
                int retryDelayMilliseconds = Mathf.CeilToInt(delaySeconds * 1000f);
                if (retryDelayMilliseconds > 0)
                {
                    await Task.Delay(retryDelayMilliseconds, cancellationToken);
                }
            }

            if (!connected)
            {
                ShutdownReason reason = attemptShutdownReason;
                Debug.LogWarning($"[{nameof(SessionConnectionCoordinator)}] Reconnect to raid failed. AttemptShutdownReason={reason}; AttemptThrew={attemptThrew}.", this);

                if (ShouldDiscardReconnectTicket(reason, attemptThrew))
                {
                    Debug.LogWarning(
                        $"[HM-MULTI] FIX-51 reconnect rejected: ProfileId={ticket.ProfileId.Value}, Reason={reason}; " +
                        "ticket discarded, returning to Town.",
                        this);
                    DiscardActiveReconnectTicket();
                }

                return await RecoverTownAfterFailedReconnectAsync();
            }

            TransitionTo(SessionConnectionState.Raid);
            _raidAdmissionConfirmed = true;
            _loadoutConfirmationPending = false;
            _currentRaidCode = ticket.RaidCode;
            _currentProfileId = ticket.ProfileId;
            _currentRaidGenerationId = ticket.RaidGenerationId;
            _reservationParticipationRaidId = _currentRaidGenerationId;
            _currentSessionName = ticket.SessionName;
            _currentRole = RaidConnectionRole.Client;
            _activeReconnectTicket = null;
            RaidReconnectTicketStore.Save(ticket);
            ReconnectTicketChanged?.Invoke(null);
            _pendingTransitionFailure = null;
            ResetHostResultsReturn();

            return SessionTransitionResult.Succeeded;
        }
        finally
        {
            EndTransitionCancellation();
            _operationActive = false;
        }
    }

    private async Task<SessionTransitionResult> RecoverTownAfterFailedReconnectAsync()
    {
        TransitionTo(SessionConnectionState.ReturningTown);
        await ShutdownActiveRunnersAsync();

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == _townSceneName)
        {
            var loadOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(_townSceneName);
            while (loadOp != null && !loadOp.isDone)
            {
                await Task.Yield();
            }
        }

        CancellationToken cancellationToken = _isQuitting ? new CancellationToken(true) : CancellationToken.None;
        bool recovered = await _hubLauncher.StartHubSessionAsync(_townSceneName, cancellationToken);
        if (!recovered)
        {
            TransitionTo(SessionConnectionState.Failed);
            return SessionTransitionResult.RecoveryFailed;
        }

        TransitionTo(SessionConnectionState.Town);
        CompleteTownEntry(SessionTransitionResult.ConnectionFailed);
        return SessionTransitionResult.ConnectionFailed;
    }

    public static bool ShouldDiscardReconnectTicket(ShutdownReason reason, bool attemptThrew) =>
        !attemptThrew && IsTerminalReconnectShutdownReason(reason);

    public static bool IsTerminalReconnectShutdownReason(ShutdownReason reason) =>
        reason == ShutdownReason.GameNotFound ||
        reason == ShutdownReason.GameClosed ||
        reason == ShutdownReason.ConnectionRefused;

    public void DiscardActiveReconnectTicket()
    {
        bool hadTicket = _activeReconnectTicket.HasValue;
        // Explicit discard (including terminal reconnect refusal) ends the retained participation.
        if (hadTicket) ClearLostParticipationReservation();
        _activeReconnectTicket = null;
        _currentRaidCode = default;
        _currentProfileId = default;
        _currentRaidGenerationId = null;
        _currentSessionName = null;
        RaidReconnectTicketStore.Clear();
        if (hadTicket)
        {
            ReconnectTicketChanged?.Invoke(null);
        }
    }

    /// <summary>
    /// Replaces the current raid runner with a fresh Shared Mode Town runner.
    /// It also provides the explicit recovery entry point from a failed transition.
    /// </summary>
    public Task<SessionTransitionResult> ReturnToTownAsync()
    {
        return ReturnToTownInternalAsync(isParticipantReturn: false);
    }

    /// <summary>
    /// Returns one terminal Client participant to Town. The operational Host is rejected
    /// because Host Migration is reserved for unexpected peer loss.
    /// </summary>
    public Task<SessionTransitionResult> ReturnParticipantToTownAsync()
    {
        return ReturnToTownInternalAsync(isParticipantReturn: true);
    }

    /// <summary>
    /// Gracefully shuts down the active Town runner (if any) and transitions back to the MainMenu state.
    /// This is valid from the Town or Failed states.
    /// </summary>
    public async Task<SessionTransitionResult> ReturnToMainMenuAsync()
    {
        if (State != SessionConnectionState.Town && State != SessionConnectionState.Failed)
        {
            Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] Cannot return to MainMenu from state {State}.", this);
            return SessionTransitionResult.InvalidState;
        }

        if (_operationActive)
        {
            Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] A transition is already active.", this);
            return SessionTransitionResult.Busy;
        }

        _operationActive = true;
        try
        {
            if (_hubLauncher != null)
            {
                await _hubLauncher.ShutdownAndDestroyRunnerAsync();
            }

            if (!_stateMachine.TryTransition(SessionConnectionState.MainMenu))
            {
                Debug.LogError($"[{nameof(SessionConnectionCoordinator)}] Failed state transition to MainMenu.", this);
                return SessionTransitionResult.InvalidState;
            }

            DiscardActiveReconnectTicket();
            CompleteTownEntry();
            _pendingPartyContinuation = null;
            return SessionTransitionResult.Succeeded;
        }
        finally
        {
            _operationActive = false;
        }
    }

    private async Task<SessionTransitionResult> ReturnToTownInternalAsync(
        bool isParticipantReturn)
    {
        if (_isQuitting)
        {
            return SessionTransitionResult.ConnectionFailed;
        }

        if (_operationActive)
        {
            return SessionTransitionResult.Busy;
        }

        if (!IsSceneEnabled(_townSceneName))
        {
            return SessionTransitionResult.InvalidRequest;
        }

        if (State != SessionConnectionState.Raid && State != SessionConnectionState.Failed)
        {
            return SessionTransitionResult.InvalidState;
        }

        NetworkRunner activeRaidRunner = _raidLauncher != null
            ? _raidLauncher.Runner
            : null;
        if (isParticipantReturn && activeRaidRunner != null && activeRaidRunner.IsServer)
        {
            Debug.LogWarning(
                "[HM-MULTI] Operational Host participant return rejected. " +
                "Host Migration requires unexpected Host peer loss.",
                this);
            return SessionTransitionResult.InvalidRequest;
        }

        _operationActive = true;
        CancellationToken cancellationToken = BeginTransitionCancellation();
        try
        {
            if (_loadoutConfirmationPending && !TryConfirmActiveReservation())
            {
                return SessionTransitionResult.LoadoutConfirmationFailed;
            }

            // A local extraction receipt survives both backend ACK and runner destruction.
            // Every other explicit Return is a loss, including defeat, abandon and closure.
            CaptureTechnicalClosureLossNotice();
            ClearLostParticipationReservation();
            NetworkRunner raidRunner = _raidLauncher.Runner;
            Debug.Log(
                $"[HM-MULTI] Ordinary Town return requested. " +
                $"ParticipantReturn={isParticipantReturn}, " +
                $"IsServer={raidRunner != null && raidRunner.IsServer}.",
                this);

            TransitionTo(SessionConnectionState.ReturningTown);
            UpdateTicketState(SessionConnectionState.ReturningTown);

            bool shutdownSucceeded = await ShutdownActiveRunnersAsync();
            if (_isQuitting)
            {
                return SessionTransitionResult.ConnectionFailed;
            }

            if (!shutdownSucceeded)
            {
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.ShutdownFailed;
            }

            bool started = await _hubLauncher.StartHubSessionAsync(_townSceneName, cancellationToken);
            if (_isQuitting)
            {
                return SessionTransitionResult.ConnectionFailed;
            }

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

            DiscardActiveReconnectTicket();
            CompleteTownEntry();
            return SessionTransitionResult.Succeeded;
        }
        catch (OperationCanceledException) when (_isQuitting)
        {
            return SessionTransitionResult.ConnectionFailed;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            TransitionTo(SessionConnectionState.Failed);
            return SessionTransitionResult.RecoveryFailed;
        }
        finally
        {
            EndTransitionCancellation();
            _operationActive = false;
        }
    }

    /// <summary>
    /// Preserves the existing MainMenu raid lobby as an explicit development-only route.
    /// The coordinator still owns the runner so the single-runner invariant is maintained.
    /// </summary>
    public async Task<SessionTransitionResult> StartDirectRaidForDevelopmentAsync(
        string sessionName,
        GameMode mode)
    {
        if (_isQuitting)
        {
            return SessionTransitionResult.ConnectionFailed;
        }

        if (_operationActive)
        {
            return SessionTransitionResult.Busy;
        }

        if ((mode != GameMode.Host && mode != GameMode.Client) ||
            string.IsNullOrWhiteSpace(sessionName))
        {
            return SessionTransitionResult.InvalidRequest;
        }

        if (State != SessionConnectionState.MainMenu && State != SessionConnectionState.Failed)
        {
            return SessionTransitionResult.InvalidState;
        }

        _operationActive = true;
        CancellationToken cancellationToken = BeginTransitionCancellation();
        try
        {
            ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
            if (!TryBuildDevelopmentLaunchContext(
                    sessionName,
                    localProfile,
                    mode,
                    out RaidLaunchContext launchContext))
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
                    out PendingLoadoutReservation reservation) != StashOperationResult.Success)
            {
                return SessionTransitionResult.LoadoutReservationFailed;
            }

            RaidConnectionRole role = mode == GameMode.Host ? RaidConnectionRole.Host : RaidConnectionRole.Client;
            var request = new RaidConnectionRequest(launchContext.RaidCode, role);
            _activeTicket = new RaidTransitionTicket(
                request,
                reservation,
                SessionConnectionState.ConnectingRaid,
                launchContext);
            TransitionTo(SessionConnectionState.ConnectingRaid);
            bool shutdownSucceeded = await ShutdownActiveRunnersAsync();
            if (_isQuitting)
            {
                return SessionTransitionResult.ConnectionFailed;
            }

            if (!shutdownSucceeded)
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
                _gameplaySceneName,
                launchContext,
                reservation,
                cancellationToken);
            if (_isQuitting)
            {
                return SessionTransitionResult.ConnectionFailed;
            }

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
            UpdateTicketState(SessionConnectionState.Raid);
            _raidAdmissionConfirmed = true;
            _loadoutConfirmationPending = true;
            _currentRaidCode = launchContext.RaidCode;
            _currentProfileId = launchContext.LocalProfileId;
            _currentRaidGenerationId = launchContext.RaidCode.RaidId;
            _reservationParticipationRaidId = _currentRaidGenerationId;
            _currentSessionName = sessionName;
            _currentRole = role;
            TryConfirmActiveReservation();
            ResetHostResultsReturn();
            _pendingTransitionFailure = null;
            return SessionTransitionResult.Succeeded;
        }
        catch (OperationCanceledException)
        {
            if (this == null || _isQuitting)
            {
                return SessionTransitionResult.ConnectionFailed;
            }

            if (!TryRollbackActiveReservation())
            {
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.LoadoutRollbackFailed;
            }

            TransitionTo(SessionConnectionState.Failed);
            return SessionTransitionResult.ConnectionFailed;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (!TryRollbackActiveReservation())
            {
                TransitionTo(SessionConnectionState.Failed);
                return SessionTransitionResult.LoadoutRollbackFailed;
            }

            TransitionTo(SessionConnectionState.Failed);
            return SessionTransitionResult.ConnectionFailed;
        }
        finally
        {
            EndTransitionCancellation();
            _operationActive = false;
        }
    }

    private async Task<SessionTransitionResult> RecoverTownAfterRaidFailureAsync(
        SessionTransitionResult failure)
    {
        if (_isQuitting)
        {
            return failure;
        }

        // [AUDIT] Log entry conditions for recovery
        ApplicationStashContext auditStash = FindAnyObjectByType<ApplicationStashContext>();
        Debug.LogWarning(
            $"[AUDIT][SessionConnectionCoordinator] RecoverTownAfterRaidFailureAsync entered. " +
            $"Failure={failure}. RaidAdmissionConfirmed={_raidAdmissionConfirmed}. " +
            $"HasTicket={_activeTicket.HasValue}. " +
            $"HasReservation={(_activeTicket.HasValue && _activeTicket.Value.HasLoadoutReservation)}. " +
            $"StashContext={(auditStash != null ? "found" : "NULL")}. " +
            $"LoadoutService={(auditStash?.LoadoutService != null ? "ok" : "null")}.",
            this);

        if (!TryRollbackActiveReservation())
        {
            TransitionTo(SessionConnectionState.Failed);
            return SessionTransitionResult.LoadoutRollbackFailed;
        }

        return await RestoreTownAfterRaidFailureAsync(failure);
    }

    private async Task<SessionTransitionResult> RestoreTownAfterRaidFailureAsync(SessionTransitionResult failure)
    {
        _pendingTransitionFailure = failure;
        TransitionTo(SessionConnectionState.ReturningTown);
        UpdateTicketState(SessionConnectionState.ReturningTown);

        bool shutdownSucceeded = await ShutdownActiveRunnersAsync();
        if (_isQuitting)
        {
            return failure;
        }

        if (!shutdownSucceeded)
        {
            TransitionTo(SessionConnectionState.Failed);
            return SessionTransitionResult.RecoveryFailed;
        }

        CancellationToken cancellationToken = _activeTransitionCancellation?.Token ?? CancellationToken.None;
        bool recovered = await _hubLauncher.StartHubSessionAsync(_townSceneName, cancellationToken);
        if (_isQuitting)
        {
            return failure;
        }

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

        CompleteTownEntry(failure);
        return failure;
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
        // Recovery after admission must never restore the Town ownership. The transition
        // out of Raid below consumes the backend reservation as a loss instead.
        if (_raidAdmissionConfirmed)
            return !_loadoutConfirmationPending || TryConfirmActiveReservation();

        if (!_activeTicket.HasValue || !_activeTicket.Value.HasLoadoutReservation)
        {
            // [AUDIT] No reservation to roll back — this is OK
            Debug.Log($"[AUDIT][SessionConnectionCoordinator] TryRollbackActiveReservation: no active reservation. HasValue={_activeTicket.HasValue}.", this);
            return true;
        }

        ApplicationStashContext stashContext = FindAnyObjectByType<ApplicationStashContext>();

        // [AUDIT] Log state of ApplicationStashContext at rollback time
        Debug.Log(
            $"[AUDIT][SessionConnectionCoordinator] TryRollbackActiveReservation. " +
            $"StashContext={(stashContext != null ? "found" : "NULL")}. " +
            $"LoadoutService={(stashContext?.LoadoutService != null ? "ok" : "null")}. " +
            $"ReservationId={_activeTicket.Value.LoadoutReservation.ReservationId}.",
            this);

        if (stashContext == null || stashContext.LoadoutService == null)
        {
            Debug.LogError(
                "[AUDIT][SessionConnectionCoordinator] TryRollbackActiveReservation FAILED: " +
                $"ApplicationStashContext is {(stashContext == null ? "null" : "present but LoadoutService is null")}",
                this);
            return false;
        }

        ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        StashOperationResult result = stashContext.LoadoutService.TryRollbackLoadoutReservation(
            localProfile,
            _activeTicket.Value.LoadoutReservation.ReservationId);
            
        Debug.Log($"[AUDIT][SessionConnectionCoordinator] Rollback result: {result}.", this);

        // InvalidInventory means the reservation is already gone (rolled back on a prior attempt).
        // Treat this as success — the reservation state is already clean.
        if (result == StashOperationResult.InvalidInventory)
        {
            Debug.Log("[AUDIT][SessionConnectionCoordinator] Reservation already absent — treating rollback as success (idempotent).", this);
            return true;
        }

        if (result == StashOperationResult.Success)
        {
            var remoteInventoryService = stashContext.GetComponent<RemoteInventoryService>();
            if (remoteInventoryService != null)
            {
                _ = remoteInventoryService.RollbackPendingReservationAsync();
            }
        }

        return result == StashOperationResult.Success;
    }

    private bool TryBuildDevelopmentLaunchContext(
        string sessionName,
        ProfileId localProfile,
        GameMode mode,
        out RaidLaunchContext launchContext)
    {
        launchContext = null;
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

        if (!RaidCode.TryParse(sessionName, out RaidCode raidCode) &&
            !RaidCode.TryParse(_developmentRaidCode, out raidCode))
        {
            return false;
        }

        var participants = new RaidLaunchParticipant[profiles.Count];
        for (int index = 0; index < profiles.Count; index++)
        {
            int teamNumber = _developmentTeamSize > 0 ? (index / _developmentTeamSize) + 1 : 1;
            RaidTeamId.TryCreate(teamNumber, out RaidTeamId teamId);
            participants[index] = new RaidLaunchParticipant(profiles[index], teamId);
        }

        return RaidLaunchContext.TryCreate(
            raidCode,
            hostProfile,
            participants,
            localProfile,
            Mathf.Max(1, _developmentLaunchSequence),
            out launchContext,
            _developmentFriendlyFireEnabled);
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
            var remoteInventoryService = stashContext.GetComponent<RemoteInventoryService>();
            if (remoteInventoryService != null)
            {
                _ = MarkReservationAdmittedAsync(remoteInventoryService,
                    _activeTicket.Value.LoadoutReservation.ReservationId);
            }
            _loadoutConfirmationPending = false;
            return true;
        }

        return false;
    }

    private async Task MarkReservationAdmittedAsync(RemoteInventoryService service, string reservationId)
    {
        var (success, error) = await service.MarkPendingReservationAdmittedAsync(reservationId);
        if (!success)
        {
            Debug.LogWarning($"[{nameof(SessionConnectionCoordinator)}] Reservation admission failed: {error.error} - {error.message}");
        }
    }

    private void ClearLostParticipationReservation()
    {
        string raidId = _reservationParticipationRaidId;
        if (string.IsNullOrEmpty(raidId)) return;
        // Consume the local departure guard before starting asynchronous work. Repeated Return,
        // Failed-state recovery and reconnect-ticket disposal cannot issue a second loss clear.
        _reservationParticipationRaidId = null;
        if (HasCommittedLocalExtraction(raidId)) return;

        ApplicationStashContext context = ResolveRecoveryStashContext();
        RemoteInventoryService service = context != null ? context.GetComponent<RemoteInventoryService>() : null;
        if (service == null || context.Store == null || context.Store.ProfileId != _currentProfileId)
        {
            Debug.LogWarning($"[{nameof(SessionConnectionCoordinator)}] Loss cleanup unavailable; login will recover the orphan reservation.");
            return;
        }
        _participationOutcomeTask = ClearLostParticipationReservationAsync(service);
    }

    private bool HasCommittedLocalExtraction(string raidId)
    {
        ApplicationStashContext context = ResolveRecoveryStashContext();
        return context?.Store != null && context.Store.ProfileId == _currentProfileId &&
               context.Store.HasCommittedExtraction(raidId);
    }

    /// <summary>Consumes the pending Town loss notice once; the caller owns presenting it.</summary>
    public bool TryConsumePendingLossNotice(out RaidLossNotice notice) => _lossNotice.TryConsume(out notice);

    private void CaptureTechnicalClosureLossNotice()
    {
        NetworkRunner runner = _raidLauncher != null ? _raidLauncher.Runner : null;
        if (runner == null || !runner.IsRunning || !TryResolveMatchController(runner, out NetworkMatchController match))
        {
            return;
        }

        RaidLossNoticeReason? reason = RaidLossNoticePolicy.ReasonForClosure(match.ClosureReason);
        if (reason.HasValue)
        {
            CaptureLossNotice(reason.Value);
        }
    }

    /// <summary>
    /// Records what the departure is about to consume. It reads the application-owned reservation, so it
    /// must run before the loss clear and works after the runner is gone.
    /// </summary>
    private void CaptureLossNotice(RaidLossNoticeReason reason)
    {
        string raidId = _reservationParticipationRaidId;
        if (string.IsNullOrEmpty(raidId) || State != SessionConnectionState.Raid ||
            HasCommittedLocalExtraction(raidId))
        {
            return;
        }

        RaidLossNotice notice = null;
        string error = "No reservation snapshot.";
        if (!_activeTicket.HasValue || !_activeTicket.Value.HasLoadoutReservation ||
            !RaidLossNotice.TryCreate(reason, _activeTicket.Value.LoadoutReservation, out notice, out error))
        {
            Debug.LogWarning($"[FIX-53] Loss notice not captured. Reason={reason}, Error={error}", this);
            return;
        }

        if (_lossNotice.Capture(notice))
        {
            Debug.Log(
                $"[FIX-53] Loss notice captured. Reason={reason}, Items={notice.Lost.Entries.Count}.",
                this);
        }
    }

    private async Task ClearLostParticipationReservationAsync(RemoteInventoryService service)
    {
        var (success, error) = await service.ClearPendingReservationAsync();
        if (!success && error.error != "EXTRACTION_RESULT_PENDING")
        {
            Debug.LogWarning($"[{nameof(SessionConnectionCoordinator)}] Reservation loss cleanup failed: {error.error} - {error.message}");
        }
    }

    private async Task<bool> ShutdownActiveRunnersAsync()
    {
        // Do not enable a new Town launch while an old loss clear is still in flight.
        await _participationOutcomeTask;
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
        Debug.Log(
            $"[HM-MULTI] Raid runner shutdown observed. " +
            $"Reason={shutdownReason}, State={State}, OperationActive={_operationActive}.",
            this);

        if (!ShouldRecoverRaidShutdown(
                _operationActive,
                _isQuitting,
                State,
                shutdownReason,
                _outgoingHostHandoff))
        {
            if (shutdownReason == ShutdownReason.HostMigration && State == SessionConnectionState.Raid)
            {
                _hostMigrationSourceRunner = runner;
                Debug.Log(
                    "[HM-MULTI] Town recovery suppressed; Host Migration owns runner replacement.",
                    this);
            }
            return;
        }

        // Failed migration is reported with Error on the original source runner. It is
        // definitive, unlike an ordinary Client transport disconnect from a live Raid.
        bool migrationFailed = _hostMigrationSourceRunner == runner &&
            shutdownReason == ShutdownReason.Error;
        _hostMigrationSourceRunner = null;
        bool outgoingHandoff = _outgoingHostHandoff && shutdownReason == ShutdownReason.HostMigration;
        _outgoingHostHandoff = false;
        if (outgoingHandoff)
        {
            Debug.Log("[HM-MULTI] FIX-50 outgoing Host handed off; returning to Town.", this);
        }
        RecoverFromUnexpectedShutdown(
            preserveReconnect: !outgoingHandoff && !migrationFailed &&
                !IsTerminalReconnectShutdownReason(shutdownReason),
            outgoingHandoff: outgoingHandoff);
    }

    private async void RecoverFromUnexpectedShutdown(
        bool preserveReconnect = true,
        bool outgoingHandoff = false)
    {
        if (_isQuitting)
        {
            return;
        }

        if (_operationActive)
        {
            TransitionTo(SessionConnectionState.Failed);
            return;
        }

        _operationActive = true;
        CancellationToken cancellationToken = BeginTransitionCancellation();
        try
        {
            if (!preserveReconnect)
            {
                ReportConnectionLoss(outgoingHandoff, participationRetained: false);
                ClearLostParticipationReservation();
                DiscardActiveReconnectTicket();
            }
            if (preserveReconnect && State == SessionConnectionState.Raid &&
                _raidAdmissionConfirmed && _currentRole == RaidConnectionRole.Client)
            {
                string genId = _currentRaidGenerationId;
                if (_raidLauncher != null && _raidLauncher.MatchController != null)
                {
                    string matchGenId = _raidLauncher.MatchController.RaidGenerationId.ToString();
                    if (!string.IsNullOrEmpty(matchGenId))
                    {
                        genId = matchGenId;
                    }
                }

                if (_currentRaidCode.IsValid && _currentProfileId.IsValid &&
                    !string.IsNullOrEmpty(genId) && !string.IsNullOrEmpty(_currentSessionName))
                {
                    string activeReservationId =
                        _activeTicket.HasValue && _activeTicket.Value.LoadoutReservation != null
                            ? _activeTicket.Value.LoadoutReservation.ReservationId
                            : null;
                    string persistedReservationId =
                        RaidReconnectTicketStore.TryLoad(_currentProfileId, out RaidReconnectTicket persisted) &&
                        persisted.RaidCode == _currentRaidCode
                            ? persisted.ReservationId
                            : null;
                    _activeReconnectTicket = RaidReconnectPolicy.BuildTicketAtUnexpectedShutdown(
                        _currentRaidCode,
                        _currentProfileId,
                        genId,
                        _currentSessionName,
                        activeReservationId,
                        persistedReservationId);
                    Debug.Log(
                        $"[HM-MULTI] FIX-51 reconnect proof: ticket preserved. ProfileId={_currentProfileId.Value}, " +
                        $"HasReservationId={RaidReconnectPolicy.DescribeProof(_activeReconnectTicket.Value.ReservationId)}.",
                        this);
                    RaidReconnectTicketStore.Save(_activeReconnectTicket.Value);
                    ReconnectTicketChanged?.Invoke(_activeReconnectTicket);
                    Debug.Log($"[{nameof(SessionConnectionCoordinator)}] Reconnect ticket preserved for {_currentProfileId.Value} in session {_currentSessionName}.", this);
                }
            }

            if (preserveReconnect)
            {
                // Leaving Raid without a retained ticket clears the reservation as a loss below.
                ReportConnectionLoss(outgoingHandoff, _activeReconnectTicket.HasValue);
            }

            TransitionTo(SessionConnectionState.ReturningTown);
            UpdateTicketState(SessionConnectionState.ReturningTown);
            bool shutdownSucceeded = await ShutdownActiveRunnersAsync();
            if (_isQuitting)
            {
                return;
            }

            if (!shutdownSucceeded)
            {
                TransitionTo(SessionConnectionState.Failed);
                return;
            }

            bool started = await _hubLauncher.StartHubSessionAsync(_townSceneName, cancellationToken);
            if (_isQuitting)
            {
                return;
            }

            if (!started)
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

            CompleteTownEntry();
        }
        catch (Exception exception)
        {
            if (this == null || _isQuitting)
            {
                return;
            }

            Debug.LogException(exception, this);
            TransitionTo(SessionConnectionState.Failed);
        }
        finally
        {
            EndTransitionCancellation();
            _operationActive = false;
        }
    }

    private void ReportConnectionLoss(bool isOutgoingHostHandoff, bool participationRetained)
    {
        if (RaidLossNoticePolicy.ShouldReportConnectionLoss(isOutgoingHostHandoff, participationRetained))
        {
            CaptureLossNotice(RaidLossNoticeReason.ConnectionLost);
        }
    }

    private bool TransitionTo(SessionConnectionState nextState)
    {
        SessionConnectionState previousState = State;
        if (!_stateMachine.TryTransition(nextState))
        {
            Debug.LogError($"Invalid session transition: {State} -> {nextState}.", this);
            return false;
        }

        if (previousState == SessionConnectionState.Raid &&
            nextState != SessionConnectionState.Raid)
        {
            // Unexpected Client disconnects retain a reconnectable participation. Only a
            // definitive ending (or explicit ticket discard) may consume its reservation.
            if (!_activeReconnectTicket.HasValue) ClearLostParticipationReservation();
            ResetHostResultsReturn();
        }

        StateChanged?.Invoke(nextState);
        return true;
    }

    private CancellationToken BeginTransitionCancellation()
    {
        EndTransitionCancellation();
        _activeTransitionCancellation = new CancellationTokenSource();
        return _activeTransitionCancellation.Token;
    }

    private void EndTransitionCancellation()
    {
        _activeTransitionCancellation?.Dispose();
        _activeTransitionCancellation = null;
    }

    private void CompleteTownEntry(SessionTransitionResult? pendingFailure = null)
    {
        // Runner-owned registries, input, HUD and camera bindings disappear with the old
        // runner object. This is the persistent owner's single Raid-scoped cleanup boundary.
        _launchReservationSave = null;
        _activeTicket = null;
        _acknowledgedLaunchSequence = 0;
        _launchDispatchActive = false;
        _raidAdmissionConfirmed = false;
        _loadoutConfirmationPending = false;
        ResetHostResultsReturn();
        _pendingTransitionFailure = pendingFailure;
    }

    private void ResetHostResultsReturn()
    {
        _hostResultsReturnRequested = false;
        _hostResultsReturnStarted = false;
        _outgoingHostHandoff = false;
        _hostHandoffNotBefore = 0f;
        _lastHostHandoffOutcome = HostHandoffOutcome.Stayed;
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
