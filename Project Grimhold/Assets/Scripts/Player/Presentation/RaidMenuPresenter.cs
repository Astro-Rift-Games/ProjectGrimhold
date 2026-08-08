using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orchestrates the local in-raid pause and defeat menu overlay.
/// Manages local gameplay input suppression, displays basic controls and defeat state,
/// and handles session abandonment through <see cref="NetworkRunner.Shutdown"/> and returning to MainMenu.
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

    private IDisposable _inputSuppression;
    private bool _isBound;
    private bool _isSubscribed;
    private bool _wasDefeatedObserved;
    private bool _isAbandoning;

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
        _isBound = false;
        _wasDefeatedObserved = false;
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
    /// Shuts down the active Fusion session before returning to MainMenu.
    /// </summary>
    public async Task AbandonRaidAsync()
    {
        if (!_isBound || _runner == null || _isAbandoning)
        {
            return;
        }

        _isAbandoning = true;
        NetworkRunner runnerToShutdown = _runner;
        Unbind();

        if (runnerToShutdown.IsRunning)
        {
            if (runnerToShutdown.IsServer)
            {
                // Delay briefly to allow the final snapshot (e.g., death state) to be uploaded to the cloud
                // before severing the connection, minimizing rollback severity on Host Migration.
                await Task.Delay(1000);
            }
            await runnerToShutdown.Shutdown();
        }

        SceneManager.LoadScene("MainMenu");
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
        if (_wasDefeatedObserved)
        {
            return;
        }

        CloseMenu();
    }

    private async void OnAbandonRequested()
    {
        try
        {
            await AbandonRaidAsync();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void RefreshViewContent()
    {
        if (_view == null)
        {
            return;
        }

        if (_wasDefeatedObserved)
        {
            _view.PresentDefeatedState();
        }
        else
        {
            _view.PresentAliveState();
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
