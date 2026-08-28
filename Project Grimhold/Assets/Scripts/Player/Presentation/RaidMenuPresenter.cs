using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// Orchestrates local raid menu, defeated-player input blocking, and spectator presentation.
/// Authoritative participant results remain owned by NetworkRaidParticipant and runner lifecycle
/// remains owned by SessionConnectionCoordinator.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidMenuPresenter : MonoBehaviour
{
    private const float NoTargetRefreshSeconds = 0.5f;

    [SerializeField] private RaidMenuView _view;

    private PlayerCharacter _character;
    private PlayerInputReader _inputReader;
    private RaidInventoryPresenter _inventoryPresenter;
    private NetworkRunner _runner;
    private NetworkRaidParticipant _participant;
    private PlayerExpeditionProgressionResolver _progressionResolver;
    private PlayerExtractionLootSaver _extractionLootSaver;
    private LocalRaidSpectatorController _spectator;

    private IDisposable _inputSuppression;
    private bool _isBound;
    private bool _isSubscribed;
    private bool _wasDefeatedObserved;
    private bool _awaitingAbandonConfirmation;
    private bool _returnRequested;
    private bool _returnStarted;
    private bool _hostClosureAcknowledged;
    private bool _hasProgressionResultSnapshot;
    private ExpeditionProgressionResult _progressionResultSnapshot;
    private float _nextNoTargetRefreshAt;
    private RaidParticipantState _observedParticipantState;
    private bool _observedExtractionFinalizationComplete;
    private bool _observedProgressionCommitConfirmed;
    private bool _observedHasLocalProgressionCommitResult;
    private ProgressionCommitResult _observedLocalProgressionCommitResult;
    private ExtractionLootSaveStatus _observedSaveStatus;
    private NetworkMatchController.MatchPhase _observedMatchPhase;

    public bool IsOpen => _isBound && _view != null && _view.IsOpen;

    public void Bind(
        PlayerCharacter character,
        PlayerInputReader inputReader,
        RaidInventoryPresenter inventoryPresenter,
        NetworkRunner runner)
    {
        Unbind();
        if (character == null || inputReader == null || runner == null || _view == null)
        {
            Debug.LogError($"{nameof(RaidMenuPresenter)} has missing binding dependencies or view.", this);
            return;
        }

        _character = character;
        _inputReader = inputReader;
        _inventoryPresenter = inventoryPresenter;
        _runner = runner;
        _extractionLootSaver = character.GetComponent<PlayerExtractionLootSaver>();
        RaidAvatarParticipantLink participantLink = character.GetComponent<RaidAvatarParticipantLink>();
        if (participantLink != null && !participantLink.TryResolveParticipant(out _participant))
        {
            Debug.LogError($"{nameof(RaidMenuPresenter)} could not resolve {nameof(NetworkRaidParticipant)}.", this);
            Unbind();
            return;
        }

        if (_participant != null)
        {
            _progressionResolver =
                _participant.GetComponent<PlayerExpeditionProgressionResolver>();
            if (_progressionResolver == null)
            {
                Debug.LogError(
                    $"{nameof(RaidMenuPresenter)} could not resolve " +
                    $"{nameof(PlayerExpeditionProgressionResolver)}.",
                    this);
                Unbind();
                return;
            }

            _spectator = new LocalRaidSpectatorController(_runner, _participant);
        }

        _isBound = true;
        _wasDefeatedObserved = _participant != null
            ? _participant.State == RaidParticipantState.Defeated
            : !_character.IsAlive;
        _observedParticipantState = _participant != null
            ? _participant.State
            : RaidParticipantState.Raiding;
        _observedExtractionFinalizationComplete = _participant != null &&
            _participant.IsExtractionProgressionComplete;
        _observedProgressionCommitConfirmed = _participant != null &&
            _participant.IsProgressionCommitConfirmed;
        _observedHasLocalProgressionCommitResult = _participant != null &&
            _participant.HasLocalProgressionCommitResult;
        _observedLocalProgressionCommitResult = _participant != null
            ? _participant.LocalProgressionCommitResult
            : default;
        _observedSaveStatus = _extractionLootSaver != null
            ? _extractionLootSaver.LocalSaveStatus
            : ExtractionLootSaveStatus.None;
        _observedMatchPhase = GetMatchPhase();

        if (isActiveAndEnabled)
        {
            Subscribe();
            ApplyInitialPresentation();
        }
    }

    public void Unbind()
    {
        Unsubscribe();
        CleanupSpectatorPresentation();
        _inventoryPresenter?.SetGameplayMutationsBlocked(false);
        CloseMenuInternal(forceReleaseSuppression: true);
        _character = null;
        _inputReader = null;
        _inventoryPresenter = null;
        _runner = null;
        _participant = null;
        _progressionResolver = null;
        _extractionLootSaver = null;
        _spectator = null;
        _isBound = false;
        _wasDefeatedObserved = false;
        _awaitingAbandonConfirmation = false;
        _returnRequested = false;
        _returnStarted = false;
        _hostClosureAcknowledged = false;
        _hasProgressionResultSnapshot = false;
        _progressionResultSnapshot = default;
        _nextNoTargetRefreshAt = 0f;
        _observedParticipantState = RaidParticipantState.Raiding;
        _observedExtractionFinalizationComplete = false;
        _observedProgressionCommitConfirmed = false;
        _observedHasLocalProgressionCommitResult = false;
        _observedLocalProgressionCommitResult = default;
        _observedSaveStatus = ExtractionLootSaveStatus.None;
        _observedMatchPhase = NetworkMatchController.MatchPhase.WaitingForPlayers;
        _view?.Clear();
    }

    public void OpenMenu()
    {
        if (!_isBound || _view == null)
        {
            return;
        }

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            Debug.LogWarning(
                $"{nameof(RaidMenuPresenter)} requires an active EventSystem for UI pointer events.",
                this);
        }

        EnsureInputSuppression();
        RefreshViewContent();
        _view.SetMenuVisible(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void CloseMenu()
    {
        CloseMenuInternal(forceReleaseSuppression: false);
    }

    public void ToggleMenu()
    {
        if (!_isBound)
        {
            return;
        }

        if (IsLocalDefeated())
        {
            if (IsOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
            return;
        }

        if (HasPersistentResultScreen())
        {
            return;
        }

        if (IsOpen)
        {
            CloseMenu();
            return;
        }

        if (_inventoryPresenter == null || !_inventoryPresenter.IsOpen)
        {
            OpenMenu();
        }
    }

    public Task AbandonRaidAsync()
    {
        if (!IsLocalDefeated())
        {
            _participant?.RequestAbandon();
        }
        return Task.CompletedTask;
    }

    private void OnEnable()
    {
        if (!_isBound)
        {
            return;
        }
        Subscribe();
        ApplyInitialPresentation();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        if (!_isBound || _character == null)
        {
            return;
        }

        ObserveCharacterState(_character.IsAlive);
        ObserveParticipantState();
        UpdateSpectator();
    }

    private void ApplyInitialPresentation()
    {
        if (IsLocalDefeated())
        {
            EnterDefeatedPresentation();
        }
        else if (HasPersistentResultScreen())
        {
            OpenMenu();
        }
        else
        {
            CloseMenu();
        }
    }

    private void ObserveCharacterState(bool isAlive)
    {
        if (_participant != null)
        {
            return;
        }

        if (!isAlive && !_wasDefeatedObserved)
        {
            _wasDefeatedObserved = true;
            EnterDefeatedPresentation();
        }
    }

    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        if (_inputReader != null)
        {
            _inputReader.MenuToggleRequested += OnMenuToggleRequested;
        }
        if (_view != null)
        {
            _view.ResumeRequested += OnResumeRequested;
            _view.AbandonRequested += OnAbandonRequested;
            _view.CancelRaidRequested += OnCancelRaidRequested;
            _view.PreviousTargetRequested += OnPreviousTargetRequested;
            _view.NextTargetRequested += OnNextTargetRequested;
        }
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        if (_inputReader != null)
        {
            _inputReader.MenuToggleRequested -= OnMenuToggleRequested;
        }
        if (_view != null)
        {
            _view.ResumeRequested -= OnResumeRequested;
            _view.AbandonRequested -= OnAbandonRequested;
            _view.CancelRaidRequested -= OnCancelRaidRequested;
            _view.PreviousTargetRequested -= OnPreviousTargetRequested;
            _view.NextTargetRequested -= OnNextTargetRequested;
        }
        _isSubscribed = false;
    }

    private void OnMenuToggleRequested() => ToggleMenu();

    private void OnResumeRequested()
    {
        if (_awaitingAbandonConfirmation)
        {
            _awaitingAbandonConfirmation = false;
            RefreshViewContent();
            return;
        }

        if (IsLocalDefeated())
        {
            EnterSpectator();
            CloseMenu();
            return;
        }

        if (_participant != null && _participant.State == RaidParticipantState.Extracted &&
            _extractionLootSaver != null &&
            _extractionLootSaver.LocalSaveStatus == ExtractionLootSaveStatus.PersistenceFailed)
        {
            _extractionLootSaver.RetryLocalCommit();
            RefreshViewContent();
            return;
        }

        CloseMenu();
    }

    private void OnAbandonRequested()
    {
        if (_participant == null)
        {
            return;
        }

        if (_participant.State == RaidParticipantState.Defeated)
        {
            if (TryResolveOperationalLocalRole(out bool defeatedIsHost) && defeatedIsHost)
            {
                RequestTerminalReturnOnce();
            }
            else if (CanDefeatedParticipantReturn())
            {
                Debug.Log("[RAID-SPECTATOR] Client Return selected.", this);
                RequestTerminalReturnOnce();
            }
            return;
        }

        if (_participant.State == RaidParticipantState.Extracted)
        {
            RequestTerminalReturnOnce();
            return;
        }

        // An operational Host cannot abandon its participant while sustaining
        // the raid. Cancel Raid is the authoritative action for that role.
        if (TryResolveOperationalLocalRole(out bool isHost) && isHost)
        {
            _awaitingAbandonConfirmation = false;
            RefreshViewContent();
            return;
        }

        if (IsVoluntaryAbandonResult())
        {
            RequestTerminalReturnOnce();
            return;
        }

        if (!_awaitingAbandonConfirmation)
        {
            _awaitingAbandonConfirmation = true;
            _view?.PresentAbandonConfirmation();
            return;
        }

        _participant.RequestAbandon();
    }

    private void OnPreviousTargetRequested()
    {
        if (_spectator != null && _spectator.SelectPrevious())
        {
            RefreshSpectatorView();
        }
    }

    private void OnNextTargetRequested()
    {
        if (_spectator != null && _spectator.SelectNext())
        {
            RefreshSpectatorView();
        }
    }

    private void RequestTerminalReturnOnce()
    {
        if (_returnRequested || !CanIssueReturnRequest())
        {
            return;
        }

        _returnRequested = true;
        if (TryResolveOperationalLocalRole(out bool isHost) && isHost)
        {
            SessionConnectionCoordinator coordinator = SessionConnectionCoordinator.Instance;
            if (coordinator == null || !coordinator.RequestHostResultsReturn())
            {
                _returnRequested = false;
            }
        }
        else
        {
            _participant.RequestReturn();
        }

        RefreshViewContent();
    }

    private void RefreshViewContent()
    {
        if (_view == null)
        {
            return;
        }

        if (_awaitingAbandonConfirmation && !HasPersistentResultScreen())
        {
            _view.PresentAbandonConfirmation();
        }
        else if (_participant == null && IsLocalDefeated())
        {
            _view.PresentDefeatedState(
                canReturn: false,
                isSpectating: false);
        }
        else if (HasPersistentResultScreen())
        {
            bool canRetryPersistence;
            string persistenceFeedback =
                GetPersistenceFeedback(out canRetryPersistence);
            bool canSpectate = IsLocalDefeated();
            bool isSpectating = _spectator != null && _spectator.IsActive;
            string title = GetTerminalResultTitle();
            if (TryCaptureProgressionResult())
            {
                _view.PresentProgressionResults(
                    title,
                    _progressionResultSnapshot,
                    persistenceFeedback,
                    !_returnRequested && CanIssueReturnRequest(),
                    _returnRequested,
                    canSpectate,
                    isSpectating,
                    canRetryPersistence);
            }
            else
            {
                _view.PresentProgressionResultsPending(
                    title,
                    persistenceFeedback,
                    canSpectate,
                    isSpectating,
                    canRetryPersistence);
            }
        }
        else
        {
            bool canAbandon = !TryResolveOperationalLocalRole(out bool isHost) || !isHost;
            _view.PresentAliveState(canAbandon);
        }

        NetworkMatchController matchController = GetMatchController();
        bool canCancelRaid = _runner != null && _runner.IsServer && matchController != null &&
            matchController.Phase == NetworkMatchController.MatchPhase.InProgress &&
            !_awaitingAbandonConfirmation && !IsLocalDefeated();
        _view.SetCancelRaidVisible(canCancelRaid);
    }

    private void OnCancelRaidRequested()
    {
        NetworkMatchController matchController = GetMatchController();
        if (_runner == null || !_runner.IsServer || matchController == null || IsLocalDefeated())
        {
            return;
        }
        matchController.TryCancelRaid();
    }

    private void ObserveParticipantState()
    {
        if (_participant == null)
        {
            return;
        }

        bool hasOperationalRole = TryResolveOperationalLocalRole(out bool isOperationalHost);
        if (ShouldStartParticipantReturn(
                _participant.IsReturnAuthorized,
                _returnStarted,
                hasOperationalRole,
                isOperationalHost))
        {
            _returnStarted = true;
            CleanupSpectatorPresentation();
            _inventoryPresenter?.SetGameplayMutationsBlocked(false);
            CloseMenuInternal(forceReleaseSuppression: true);
            ReturnToTownAsync();
            return;
        }

        // The operational Host owns the runner: its authorization comes from the authoritative
        // closure, so the global Town return belongs to SessionConnectionCoordinator and this
        // presenter only acknowledges the closure locally, exactly once.
        if (_participant.IsReturnAuthorized && hasOperationalRole && isOperationalHost &&
            !_hostClosureAcknowledged)
        {
            _hostClosureAcknowledged = true;
            CleanupSpectatorPresentation();
            _inventoryPresenter?.SetGameplayMutationsBlocked(true);
            CloseMenuInternal(forceReleaseSuppression: true);
        }

        RaidParticipantState state = _participant.State;
        bool finalizationComplete = _participant.IsExtractionProgressionComplete;
        bool progressionCommitConfirmed =
            _participant.IsProgressionCommitConfirmed;
        bool hasLocalProgressionCommitResult =
            _participant.HasLocalProgressionCommitResult;
        ProgressionCommitResult localProgressionCommitResult =
            _participant.LocalProgressionCommitResult;
        ExtractionLootSaveStatus saveStatus = _extractionLootSaver != null
            ? _extractionLootSaver.LocalSaveStatus
            : ExtractionLootSaveStatus.None;
        NetworkMatchController.MatchPhase matchPhase = GetMatchPhase();
        bool hadProgressionResultSnapshot = _hasProgressionResultSnapshot;
        bool hasProgressionResultSnapshot =
            HasPersistentResultScreen() && TryCaptureProgressionResult();
        bool capturedResult = !hadProgressionResultSnapshot &&
            hasProgressionResultSnapshot;
        bool resultChanged = state != _observedParticipantState ||
                             finalizationComplete != _observedExtractionFinalizationComplete ||
                             progressionCommitConfirmed != _observedProgressionCommitConfirmed ||
                             hasLocalProgressionCommitResult !=
                                 _observedHasLocalProgressionCommitResult ||
                             localProgressionCommitResult !=
                                 _observedLocalProgressionCommitResult ||
                             saveStatus != _observedSaveStatus ||
                             ShouldRefreshForMatchPhase(
                                 _observedMatchPhase,
                                 matchPhase,
                                 HasPersistentResultScreen());
        _observedParticipantState = state;
        _observedExtractionFinalizationComplete = finalizationComplete;
        _observedProgressionCommitConfirmed = progressionCommitConfirmed;
        _observedHasLocalProgressionCommitResult =
            hasLocalProgressionCommitResult;
        _observedLocalProgressionCommitResult = localProgressionCommitResult;
        _observedSaveStatus = saveStatus;
        _observedMatchPhase = matchPhase;

        if (state == RaidParticipantState.Defeated && !_wasDefeatedObserved)
        {
            _wasDefeatedObserved = true;
            Debug.Log("[RAID-SPECTATOR] Local participant defeated.", this);
            EnterDefeatedPresentation();
            return;
        }

        if (state == RaidParticipantState.Extracted)
        {
            if (!IsOpen)
            {
                _inventoryPresenter?.Close();
                OpenMenu();
            }
            else if (resultChanged)
            {
                RefreshViewContent();
            }
        }

        else if (IsVoluntaryAbandonResult())
        {
            _awaitingAbandonConfirmation = false;
            _inventoryPresenter?.SetGameplayMutationsBlocked(true);
            if (!IsOpen)
            {
                _inventoryPresenter?.Close();
                OpenMenu();
            }
            else if (resultChanged || capturedResult)
            {
                RefreshViewContent();
            }
        }
        else if (IsLocalDefeated() && IsOpen && (resultChanged || capturedResult))
        {
            RefreshViewContent();
        }
    }

    private void EnterDefeatedPresentation()
    {
        _inventoryPresenter?.SetGameplayMutationsBlocked(true);
        EnsureInputSuppression();
        if (_participant != null && ShouldEnterSpectatorAutomatically())
        {
            EnterSpectator();
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }

    private void EnterSpectator()
    {
        if (_spectator == null)
        {
            return;
        }
        _spectator.Enter();
        _nextNoTargetRefreshAt = Time.unscaledTime + NoTargetRefreshSeconds;
        RefreshSpectatorView();
    }

    private void UpdateSpectator()
    {
        if (_spectator == null || !_spectator.IsActive || _returnStarted)
        {
            return;
        }

        NetworkMatchController matchController = GetMatchController();
        if (matchController != null && matchController.Phase == NetworkMatchController.MatchPhase.Finished)
        {
            CleanupSpectatorPresentation();
            _inventoryPresenter?.SetGameplayMutationsBlocked(true);
            if (!IsOpen)
            {
                OpenMenu();
            }
            else
            {
                RefreshViewContent();
            }
            return;
        }

        if (_spectator.HasTarget)
        {
            if (_spectator.RefreshCurrentTarget())
            {
                RefreshSpectatorView();
            }
            return;
        }

        if (Time.unscaledTime >= _nextNoTargetRefreshAt)
        {
            _nextNoTargetRefreshAt = Time.unscaledTime + NoTargetRefreshSeconds;
            _spectator.RefreshCurrentTarget();
            RefreshSpectatorView();
        }
    }

    private void RefreshSpectatorView()
    {
        if (_spectator == null || !_spectator.IsActive)
        {
            _view?.SetSpectatorBarVisible(false);
            return;
        }
        _view?.PresentSpectatorState(_spectator.CurrentProfileId, _spectator.HasTarget);
    }

    private void CleanupSpectatorPresentation()
    {
        _spectator?.Cleanup();
        _view?.SetSpectatorBarVisible(false);
    }

    private bool ShouldEnterSpectatorAutomatically()
    {
        return !TryResolveOperationalLocalRole(out bool isHost) || isHost;
    }

    private bool CanDefeatedParticipantReturn()
    {
        return _participant != null && _participant.State == RaidParticipantState.Defeated &&
            TryResolveOperationalLocalRole(out bool isHost) && !isHost;
    }

    private bool TryResolveOperationalLocalRole(out bool isHost)
    {
        if (_runner == null || !_runner.IsRunning)
        {
            isHost = false;
            return false;
        }

        // The operational Host is runner-scoped and can change after Host Migration.
        // The frozen launch-context Host ProfileId identifies only the historical Host.
        isHost = _runner.IsServer;
        return true;
    }

    private bool IsLocalDefeated() => _wasDefeatedObserved ||
        (_participant != null && _participant.State == RaidParticipantState.Defeated);

    private bool IsVoluntaryAbandonResult() => _participant != null &&
        _participant.State == RaidParticipantState.Aborted &&
        _participant.FinalizationCause ==
            ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed;

    private bool TryCaptureProgressionResult()
    {
        if (_hasProgressionResultSnapshot)
        {
            return true;
        }

        if (_progressionResolver == null ||
            !_progressionResolver.TryGetProgressionResult(
                out ExpeditionProgressionResult result))
        {
            return false;
        }

        _progressionResultSnapshot = result;
        _hasProgressionResultSnapshot = true;
        return true;
    }

    private bool CanIssueReturnRequest()
    {
        if (_participant == null ||
            !TryResolveOperationalLocalRole(out bool isHost))
        {
            return false;
        }

        NetworkMatchController matchController = GetMatchController();
        NetworkMatchController.MatchPhase phase = matchController != null
            ? matchController.Phase
            : NetworkMatchController.MatchPhase.WaitingForPlayers;
        bool isCompatibleClientPhase = phase == NetworkMatchController.MatchPhase.InProgress ||
                                       phase == NetworkMatchController.MatchPhase.Closing ||
                                       phase == NetworkMatchController.MatchPhase.Finished;
        if (!isHost)
        {
            return RaidResultsReturnPolicy.CanRequestClientReturn(
                _participant.State,
                _participant.FinalizationCause,
                _participant.IsExtractionProgressionComplete,
                _hasProgressionResultSnapshot,
                _participant.IsProgressionCommitConfirmed,
                isCompatibleClientPhase);
        }

        NetworkSpawnManager spawnManager = _runner.GetComponent<NetworkSpawnManager>();
        return RaidResultsReturnPolicy.CanRequestHostReturn(
            _participant.State,
            _participant.FinalizationCause,
            _participant.IsExtractionProgressionComplete,
            _hasProgressionResultSnapshot,
            _participant.IsProgressionCommitConfirmed,
            isServer: true,
            hasRaidingParticipants: spawnManager == null || spawnManager.HasRaidingParticipants,
            isMatchFinished: phase == NetworkMatchController.MatchPhase.Finished);
    }

    internal static bool CanIssueReturnRequest(
        bool hasProgressionResultSnapshot,
        bool isProgressionCommitConfirmed,
        RaidParticipantState state,
        ExpeditionProgressionFinalizationCause finalizationCause,
        bool isExtractionProgressionComplete,
        bool isHost,
        bool isCompatibleClientPhase,
        bool isMatchFinished,
        bool hasRaidingParticipants)
    {
        if (isHost)
        {
            return RaidResultsReturnPolicy.CanRequestHostReturn(
                state,
                finalizationCause,
                isExtractionProgressionComplete,
                hasProgressionResultSnapshot,
                isProgressionCommitConfirmed,
                isServer: true,
                hasRaidingParticipants,
                isMatchFinished);
        }

        return RaidResultsReturnPolicy.CanRequestClientReturn(
            state,
            finalizationCause,
            isExtractionProgressionComplete,
            hasProgressionResultSnapshot,
            isProgressionCommitConfirmed,
            isCompatibleClientPhase);
    }

    private string GetTerminalResultTitle()
    {
        if (_participant == null)
        {
            return "Resultados";
        }

        return _participant.State switch
        {
            RaidParticipantState.Extracted => "Extracción completada",
            RaidParticipantState.Defeated => "Has sido Derrotado",
            RaidParticipantState.Aborted when IsVoluntaryAbandonResult() =>
                "Incursión abandonada",
            _ => "Resultados"
        };
    }

    private string GetPersistenceFeedback(out bool canRetryPersistence)
    {
        canRetryPersistence = false;
        string progressionFeedback;
        if (_participant == null)
        {
            progressionFeedback = "Persistencia de progresión no disponible.";
        }
        else if (_participant.IsProgressionCommitConfirmed)
        {
            progressionFeedback = "Progreso guardado y confirmado.";
        }
        else if (!_participant.HasLocalProgressionCommitResult)
        {
            progressionFeedback = "Guardado de progresión pendiente.";
        }
        else
        {
            progressionFeedback = _participant.LocalProgressionCommitResult switch
            {
                ProgressionCommitResult.Success or ProgressionCommitResult.AlreadyApplied =>
                    "Progreso guardado localmente. Esperando confirmación autoritativa.",
                ProgressionCommitResult.PersistenceFailed =>
                    "No se pudo guardar la progresión. Reintentando.",
                ProgressionCommitResult.Stale =>
                    "La progresión local está desactualizada y no pudo confirmarse.",
                ProgressionCommitResult.Conflict =>
                    "La progresión local presenta un conflicto y no pudo confirmarse.",
                _ => "La progresión local no pudo confirmarse."
            };
        }

        if (_participant == null ||
            _participant.State != RaidParticipantState.Extracted)
        {
            return progressionFeedback;
        }

        ExtractionLootSaveStatus lootStatus = _extractionLootSaver != null
            ? _extractionLootSaver.LocalSaveStatus
            : (_participant.IsExtractionProgressionComplete
                ? ExtractionLootSaveStatus.Committed
                : ExtractionLootSaveStatus.Pending);
        if (!_participant.IsExtractionProgressionComplete &&
            lootStatus == ExtractionLootSaveStatus.Committed)
        {
            lootStatus = ExtractionLootSaveStatus.Pending;
        }
        string lootFeedback = lootStatus switch
        {
            ExtractionLootSaveStatus.Committed => "Botín asegurado.",
            ExtractionLootSaveStatus.PersistenceFailed =>
                "No se pudo guardar el botín. Pulsa Reintentar.",
            _ => "Guardado de botín pendiente."
        };
        canRetryPersistence =
            lootStatus == ExtractionLootSaveStatus.PersistenceFailed;
        return $"{lootFeedback}\n{progressionFeedback}";
    }

    private NetworkMatchController GetMatchController() => _runner != null
        ? _runner.GetComponent<NetworkSpawnManager>()?.MatchController
        : null;

    private NetworkMatchController.MatchPhase GetMatchPhase() =>
        GetMatchController()?.Phase ??
        NetworkMatchController.MatchPhase.WaitingForPlayers;

    /// <summary>
    /// Individual participant return is valid only for a resolved non-Host role. An unresolved
    /// role means the runner is missing or shutting down and must never start a return.
    /// </summary>
    internal static bool ShouldStartParticipantReturn(
        bool isReturnAuthorized,
        bool returnStarted,
        bool hasOperationalRole,
        bool isOperationalHost)
    {
        return isReturnAuthorized &&
               !returnStarted &&
               hasOperationalRole &&
               !isOperationalHost;
    }

    internal static bool ShouldRefreshForMatchPhase(
        NetworkMatchController.MatchPhase previous,
        NetworkMatchController.MatchPhase current,
        bool hasPersistentResultScreen) =>
        hasPersistentResultScreen && previous != current;

    private async void ReturnToTownAsync()
    {
        try
        {
            if (SessionConnectionCoordinator.Instance == null)
            {
                return;
            }

            SessionTransitionResult result =
                await SessionConnectionCoordinator.Instance.ReturnParticipantToTownAsync();
            if (this == null || result == SessionTransitionResult.Succeeded)
            {
                return;
            }

            Debug.LogError(
                $"{nameof(RaidMenuPresenter)} participant return did not complete. Result={result}.",
                this);
            RestorePresentationAfterFailedReturn();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            RestorePresentationAfterFailedReturn();
        }
    }

    /// <summary>
    /// Releases the local return latch so a failed transition never leaves the player without UI.
    /// </summary>
    private void RestorePresentationAfterFailedReturn()
    {
        _returnStarted = false;
        if (!_isBound)
        {
            return;
        }

        if (IsLocalDefeated())
        {
            EnterDefeatedPresentation();
            return;
        }

        if (!IsOpen)
        {
            OpenMenu();
            return;
        }

        RefreshViewContent();
    }

    private void EnsureInputSuppression()
    {
        if (_inputSuppression == null && _inputReader != null)
        {
            _inputSuppression = _inputReader.AcquireGameplayInputSuppression();
        }
    }

    private void ReleaseInputSuppression()
    {
        _inputSuppression?.Dispose();
        _inputSuppression = null;
    }

    private void CloseMenuInternal(bool forceReleaseSuppression)
    {
        _view?.SetMenuVisible(false);
        if (forceReleaseSuppression || !HasPersistentResultScreen())
        {
            ReleaseInputSuppression();
        }
        else
        {
            EnsureInputSuppression();
        }
    }

    private bool HasPersistentResultScreen() => IsLocalDefeated() ||
        (_participant != null && _participant.State == RaidParticipantState.Extracted) ||
        IsVoluntaryAbandonResult();
}
