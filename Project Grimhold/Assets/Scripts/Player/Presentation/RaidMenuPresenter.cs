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
    private PlayerExtractionLootSaver _extractionLootSaver;
    private LocalRaidSpectatorController _spectator;

    private IDisposable _inputSuppression;
    private bool _isBound;
    private bool _isSubscribed;
    private bool _wasDefeatedObserved;
    private bool _awaitingAbandonConfirmation;
    private bool _returnRequested;
    private bool _returnStarted;
    private float _nextNoTargetRefreshAt;
    private RaidParticipantState _observedParticipantState;
    private bool _observedExtractionCommitConfirmed;
    private ExtractionLootSaveStatus _observedSaveStatus;

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
            _spectator = new LocalRaidSpectatorController(_runner, _participant);
        }

        _isBound = true;
        _wasDefeatedObserved = _participant != null
            ? _participant.State == RaidParticipantState.Defeated
            : !_character.IsAlive;
        _observedParticipantState = _participant != null
            ? _participant.State
            : RaidParticipantState.Raiding;
        _observedExtractionCommitConfirmed = _participant != null &&
            _participant.IsExtractionCommitConfirmed;
        _observedSaveStatus = _extractionLootSaver != null
            ? _extractionLootSaver.LocalSaveStatus
            : ExtractionLootSaveStatus.None;

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
        _extractionLootSaver = null;
        _spectator = null;
        _isBound = false;
        _wasDefeatedObserved = false;
        _awaitingAbandonConfirmation = false;
        _returnRequested = false;
        _returnStarted = false;
        _nextNoTargetRefreshAt = 0f;
        _observedParticipantState = RaidParticipantState.Raiding;
        _observedExtractionCommitConfirmed = false;
        _observedSaveStatus = ExtractionLootSaveStatus.None;
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
        Unsubscribe();
        CleanupSpectatorPresentation();
        _inventoryPresenter?.SetGameplayMutationsBlocked(false);
        CloseMenuInternal(forceReleaseSuppression: true);
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
            if (CanDefeatedParticipantReturn())
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
        if (_participant == null || _returnRequested)
        {
            return;
        }
        if (_participant.State == RaidParticipantState.Defeated && !CanDefeatedParticipantReturn())
        {
            return;
        }
        if (_participant.State == RaidParticipantState.Extracted &&
            !_participant.IsExtractionCommitConfirmed)
        {
            return;
        }

        _returnRequested = true;
        _participant.RequestReturn();
    }

    private void RefreshViewContent()
    {
        if (_view == null)
        {
            return;
        }

        if (_awaitingAbandonConfirmation)
        {
            _view.PresentAbandonConfirmation();
        }
        else if (_participant != null && _participant.State == RaidParticipantState.Extracted)
        {
            ExtractionLootSaveStatus saveStatus = _extractionLootSaver != null
                ? _extractionLootSaver.LocalSaveStatus
                : (_participant.IsExtractionCommitConfirmed
                    ? ExtractionLootSaveStatus.Committed
                    : ExtractionLootSaveStatus.Pending);
            _view.PresentExtractedState(saveStatus);
        }
        else if (IsLocalDefeated())
        {
            _view.PresentDefeatedState(
                CanDefeatedParticipantReturn(),
                _spectator != null && _spectator.IsActive);
        }
        else
        {
            _view.PresentAliveState();
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

        if (_participant.IsReturnAuthorized && !_returnStarted)
        {
            _returnStarted = true;
            CleanupSpectatorPresentation();
            _inventoryPresenter?.SetGameplayMutationsBlocked(false);
            CloseMenuInternal(forceReleaseSuppression: true);
            ReturnToTownAsync();
            return;
        }

        RaidParticipantState state = _participant.State;
        bool commitConfirmed = _participant.IsExtractionCommitConfirmed;
        ExtractionLootSaveStatus saveStatus = _extractionLootSaver != null
            ? _extractionLootSaver.LocalSaveStatus
            : ExtractionLootSaveStatus.None;
        bool resultChanged = state != _observedParticipantState ||
                             commitConfirmed != _observedExtractionCommitConfirmed ||
                             saveStatus != _observedSaveStatus;
        _observedParticipantState = state;
        _observedExtractionCommitConfirmed = commitConfirmed;
        _observedSaveStatus = saveStatus;

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
            _inventoryPresenter?.SetGameplayMutationsBlocked(false);
            CloseMenuInternal(forceReleaseSuppression: true);
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

    private NetworkMatchController GetMatchController() => _runner != null
        ? _runner.GetComponent<NetworkSpawnManager>()?.MatchController
        : null;

    private async void ReturnToTownAsync()
    {
        try
        {
            if (SessionConnectionCoordinator.Instance != null)
            {
                await SessionConnectionCoordinator.Instance.ReturnParticipantToTownAsync();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            _returnStarted = false;
            if (IsLocalDefeated())
            {
                EnterDefeatedPresentation();
            }
        }
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
        (_participant != null && _participant.State == RaidParticipantState.Extracted);
}
