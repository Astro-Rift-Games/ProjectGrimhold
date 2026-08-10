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

    private IDisposable _inputSuppression;
    private bool _isBound;
    private bool _isSubscribed;
    private bool _wasDefeatedObserved;
    private bool _awaitingAbandonConfirmation;
    private bool _returnStarted;

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
        RaidAvatarParticipantLink participantLink = character.GetComponent<RaidAvatarParticipantLink>();
        if (participantLink != null && !participantLink.TryResolveParticipant(out _participant))
        {
            Debug.LogError($"{nameof(RaidMenuPresenter)} could not resolve {nameof(NetworkRaidParticipant)}.", this);
            Unbind();
            return;
        }
        _isBound = true;
        _wasDefeatedObserved = !_character.IsAlive;

        if (isActiveAndEnabled)
        {
            Subscribe();
            if (_wasDefeatedObserved)
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
        _isBound = false;
        _wasDefeatedObserved = false;
        _awaitingAbandonConfirmation = false;
        _returnStarted = false;
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
        if (_wasDefeatedObserved)
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
            _participant?.RequestReturn();
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
            _participant.RequestReturn();
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
        else if (_wasDefeatedObserved)
        {
            _view.PresentDefeatedState();
        }
        else
        {
            _view.PresentAliveState();
        }
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

        if (_participant.State == RaidParticipantState.Extracted)
        {
            OpenMenu();
            _view?.PresentDefeatedState();
        }
    }

    private async void ReturnToTownAsync()
    {
        try
        {
            if (SessionConnectionCoordinator.Instance != null)
            {
                await SessionConnectionCoordinator.Instance.ReturnToTownAsync();
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

        if (forceReleaseSuppression || !_wasDefeatedObserved)
        {
            ReleaseInputSuppression();
        }
        else
        {
            // For a defeated player, ensure input suppression remains active
            EnsureInputSuppression();
        }
    }
}
