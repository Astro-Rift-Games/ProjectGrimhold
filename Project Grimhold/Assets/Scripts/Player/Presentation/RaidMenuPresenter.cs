using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// Orchestrates the local in-raid pause and defeat menu overlay.
/// Manages local gameplay input suppression, displays basic controls and defeat state,
/// and requests individual participant results. Runner lifecycle remains owned by
/// <see cref="SessionConnectionCoordinator"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidMenuPresenter : MonoBehaviour
{
    [SerializeField]
    private RaidMenuView _view;

    private PlayerCharacter _character;
    private PlayerInputReader _inputReader;
    private RaidInventoryPresenter _inventoryPresenter;
    private NetworkRunner _runner;
    private NetworkRaidParticipant _participant;
    private PlayerExtractionLootSaver _extractionLootSaver;

    private IDisposable _inputSuppression;
    private bool _isBound;
    private bool _isSubscribed;
    private bool _wasDefeatedObserved;
    private bool _awaitingAbandonConfirmation;
    private bool _returnRequested;
    private bool _returnStarted;
    private RaidParticipantState _observedParticipantState;
    private bool _observedExtractionCommitConfirmed;
    private ExtractionLootSaveStatus _observedSaveStatus;

    /// <summary>Indicates whether the presenter is bound and the menu overlay is open.</summary>
    public bool IsOpen => _isBound && _view != null && _view.IsOpen;

    /// <summary>
    /// Binds the presenter to local player components and runner instance.
    /// </summary>
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
        _isBound = true;
        _wasDefeatedObserved = _participant != null
            ? _participant.State == RaidParticipantState.Defeated
            : !_character.IsAlive;
        _observedParticipantState = _participant != null
            ? _participant.State
            : RaidParticipantState.Raiding;
        _observedExtractionCommitConfirmed = _participant != null && _participant.IsExtractionCommitConfirmed;
        _observedSaveStatus = _extractionLootSaver != null
            ? _extractionLootSaver.LocalSaveStatus
            : ExtractionLootSaveStatus.None;

        if (isActiveAndEnabled)
        {
            Subscribe();
            if (HasPersistentResultScreen())
            {
                OpenMenu();
            }
            else
            {
                CloseMenu();
            }
        }
    }

    /// <summary>
    /// Unbinds all references, releases input suppression tokens, and hides UI.
    /// </summary>
    public void Unbind()
    {
        Unsubscribe();
        CloseMenuInternal(forceReleaseSuppression: true);
        _character = null;
        _inputReader = null;
        _inventoryPresenter = null;
        _runner = null;
        _participant = null;
        _extractionLootSaver = null;
        _isBound = false;
        _wasDefeatedObserved = false;
        _awaitingAbandonConfirmation = false;
        _returnRequested = false;
        _returnStarted = false;
        _observedParticipantState = RaidParticipantState.Raiding;
        _observedExtractionCommitConfirmed = false;
        _observedSaveStatus = ExtractionLootSaveStatus.None;
        _view?.Clear();
    }

    /// <summary>Opens the local menu and acquires gameplay input suppression.</summary>
    public void OpenMenu()
    {
        if (!_isBound || _view == null)
        {
            return;
        }

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            Debug.LogWarning($"{nameof(RaidMenuPresenter)} requires an active EventSystem in the scene to receive UI pointer events.", this);
        }

        EnsureInputSuppression();
        RefreshViewContent();
        _view.SetMenuVisible(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>Closes the local menu and releases input suppression if the player is alive.</summary>
    public void CloseMenu()
    {
        CloseMenuInternal(forceReleaseSuppression: false);
    }

    /// <summary>Toggles the local menu state, prioritizing open inventory windows.</summary>
    public void ToggleMenu()
    {
        if (!_isBound)
        {
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

        if (_inventoryPresenter != null && _inventoryPresenter.IsOpen)
        {
            return;
        }

        OpenMenu();
    }

    /// <summary>
    /// Compatibility entry point for callers that previously requested immediate runner shutdown.
    /// It now only sends the authoritative abandonment request; return is observed separately.
    /// </summary>
    public Task AbandonRaidAsync()
    {
        _participant?.RequestAbandon();
        return Task.CompletedTask;
    }

    private void OnEnable()
    {
        if (!_isBound)
        {
            return;
        }

        Subscribe();
        if (HasPersistentResultScreen())
        {
            OpenMenu();
        }
        else
        {
            CloseMenu();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
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
    }

    private void ObserveCharacterState(bool isAlive)
    {
        // The participant is the authoritative result source. Health remains a
        // compatibility fallback for the direct-development path without a participant.
        if (_participant != null)
        {
            return;
        }

        if (!isAlive && !_wasDefeatedObserved)
        {
            _wasDefeatedObserved = true;
            _inventoryPresenter?.Close();
            OpenMenu();
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
        }

        _isSubscribed = false;
    }

    private void OnMenuToggleRequested()
    {
        ToggleMenu();
    }

    private void OnResumeRequested()
    {
        if (_awaitingAbandonConfirmation)
        {
            _awaitingAbandonConfirmation = false;
            RefreshViewContent();
            return;
        }

        if (_wasDefeatedObserved)
        {
            RequestTerminalReturnOnce();
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

        if (_wasDefeatedObserved || _participant.State == RaidParticipantState.Extracted)
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

    private void RequestTerminalReturnOnce()
    {
        if (_participant == null || _returnRequested)
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
        else if (_wasDefeatedObserved ||
                 (_participant != null && _participant.State == RaidParticipantState.Defeated))
        {
            _view.PresentDefeatedState();
        }
        else
        {
            _view.PresentAliveState();
        }

        NetworkMatchController matchController = _runner != null
            ? _runner.GetComponent<NetworkSpawnManager>()?.MatchController
            : null;
        bool canCancelRaid = _runner != null && _runner.IsServer && matchController != null &&
            matchController.Phase == NetworkMatchController.MatchPhase.InProgress &&
            !_awaitingAbandonConfirmation && !_wasDefeatedObserved;
        _view.SetCancelRaidVisible(canCancelRaid);
    }

    private void OnCancelRaidRequested()
    {
        NetworkMatchController matchController = _runner != null
            ? _runner.GetComponent<NetworkSpawnManager>()?.MatchController
            : null;
        if (_runner == null || !_runner.IsServer || matchController == null)
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
            _inventoryPresenter?.Close();
            OpenMenu();
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

    private async void ReturnToTownAsync()
    {
        try
        {
            if (SessionConnectionCoordinator.Instance != null)
            {
                Debug.Log(
                    "[HOST-RETURN-MIGRATION] RaidMenuPresenter observed return authorization and is delegating participant return.",
                    this);
                await SessionConnectionCoordinator.Instance.ReturnParticipantToTownAsync();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            _returnStarted = false;
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
            // For a defeated player, ensure input suppression remains active
            EnsureInputSuppression();
        }
    }

    private bool HasPersistentResultScreen()
    {
        return _wasDefeatedObserved ||
               (_participant != null &&
                (_participant.State == RaidParticipantState.Defeated ||
                 _participant.State == RaidParticipantState.Extracted));
    }
}
