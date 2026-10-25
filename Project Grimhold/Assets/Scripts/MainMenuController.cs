using Fusion;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField]
    private SessionConnectionCoordinator _connectionCoordinator;

    [SerializeField]
    private TMP_InputField roomCodeInput;

    [SerializeField]
    private GameObject menuPanel;

    [SerializeField]
    private Button createRoomButton;

    [SerializeField]
    private Button joinRoomButton;

    [SerializeField]
    private GameObject lobbyPanel;

    [SerializeField]
    private LobbyMenuController lobbyMenu;

    [SerializeField]
    private TextMeshProUGUI _statusText;

    [Header("Login")]
    [SerializeField]
    private LoginPanelView _loginPanel;

    [SerializeField]
    private CharacterCreationPanelView _characterCreationPanel;

    [SerializeField]
    private LoginFlowController _loginFlowController;

    [Header("Session actions")]
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _logoutButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _creditsButton;
    [SerializeField] private Button _exitButton;
    [SerializeField] private SettingsPanelView _settingsPanel;
    [SerializeField] private CreditsPanelView _creditsPanel;

    private bool _isBusy;
    private ApplicationAuthContext _subscribedAuthContext;

    public Action QuitAction { get; set; } = ApplicationExit.Quit;

    private LoginFlowController LoginFlow
    {
        get
        {
            if (_loginFlowController == null)
            {
                _loginFlowController = LoginFlowController.Instance ?? FindAnyObjectByType<LoginFlowController>();
            }
            return _loginFlowController;
        }
    }

    private SessionConnectionCoordinator ConnectionCoordinator
    {
        get
        {
            if (_connectionCoordinator == null)
            {
                _connectionCoordinator = SessionConnectionCoordinator.Instance ?? FindAnyObjectByType<SessionConnectionCoordinator>();
            }
            return _connectionCoordinator;
        }
    }

    private void OnEnable()
    {
        createRoomButton.onClick.AddListener(CreateRoom);
        joinRoomButton.onClick.AddListener(JoinRoom);

        roomCodeInput.gameObject.SetActive(false);
        joinRoomButton.gameObject.SetActive(false);
        lobbyPanel.SetActive(false);
        menuPanel.SetActive(true);

        _loginButton?.onClick.AddListener(ShowLoginPanel);
        _logoutButton?.onClick.AddListener(OnLogoutButtonClicked);
        _settingsButton?.onClick.AddListener(OnSettingsButtonClicked);
        _creditsButton?.onClick.AddListener(OnCreditsButtonClicked);
        _exitButton?.onClick.AddListener(OnExitButtonClicked);

        if (_loginButton == null || _logoutButton == null || _settingsButton == null ||
            _creditsButton == null || _exitButton == null || _settingsPanel == null || _creditsPanel == null)
        {
            Debug.LogError($"[{nameof(MainMenuController)}] Assign all session action buttons and SettingsPanelView/CreditsPanelView references.", this);
        }

        _subscribedAuthContext = ApplicationAuthContext.Instance;
        if (_subscribedAuthContext != null)
        {
            _subscribedAuthContext.SessionInvalidated += RefreshSessionActions;
        }

        SetStatus(string.Empty);
        RefreshSessionActions();

        if (_characterCreationPanel != null)
        {
            _characterCreationPanel.gameObject.SetActive(false);
            _characterCreationPanel.AddCreateListener(OnCreateCharacterButtonClicked);
        }

        if (_loginPanel != null)
        {
            _loginPanel.gameObject.SetActive(false);
            _loginPanel.SetStatus(string.Empty);
            _loginPanel.AddLoginListener(OnLoginButtonClicked);
            _loginPanel.AddRegisterListener(OnRegisterButtonClicked);
            _loginPanel.AddBackListener(OnLoginBackButtonClicked);
        }
        else
        {
            Debug.LogError($"[{nameof(MainMenuController)}] No LoginPanelView assigned.");
        }
    }

    private void OnDisable()
    {
        createRoomButton.onClick.RemoveListener(CreateRoom);
        joinRoomButton.onClick.RemoveListener(JoinRoom);
        _loginButton?.onClick.RemoveListener(ShowLoginPanel);
        _logoutButton?.onClick.RemoveListener(OnLogoutButtonClicked);
        _settingsButton?.onClick.RemoveListener(OnSettingsButtonClicked);
        _creditsButton?.onClick.RemoveListener(OnCreditsButtonClicked);
        _exitButton?.onClick.RemoveListener(OnExitButtonClicked);
        if (_subscribedAuthContext != null)
        {
            _subscribedAuthContext.SessionInvalidated -= RefreshSessionActions;
        }
        _subscribedAuthContext = null;

        if (_loginPanel != null)
        {
            _loginPanel.RemoveLoginListener(OnLoginButtonClicked);
            _loginPanel.RemoveRegisterListener(OnRegisterButtonClicked);
            _loginPanel.RemoveBackListener(OnLoginBackButtonClicked);
        }

        if (_characterCreationPanel != null)
        {
            _characterCreationPanel.RemoveCreateListener(OnCreateCharacterButtonClicked);
        }
    }

    private async void OnLoginButtonClicked()
    {
        var loginFlow = LoginFlow;
        if (loginFlow == null)
        {
            Debug.LogError($"[{nameof(MainMenuController)}] LoginFlowController not assigned.");
            return;
        }

        _loginPanel.SetInteractable(false);
        _loginPanel.SetStatus("Logging in...");
        SetUIInteractable(false);

        string username = _loginPanel.Username;
        try
        {
            LoginFlowResult result;
            if (loginFlow.HasHydrationFailed && loginFlow.PendingUsername == username)
            {
                result = await loginFlow.RetryHydrationAsync();
            }
            else
            {
                result = await loginFlow.ExecuteLoginAsync(username, _loginPanel.Password);
            }
            if (this != null) HandleLoginFlowResult(result);
        }
        catch (Exception ex)
        {
            if (this == null) return;
            _loginPanel.SetStatus(ex.Message);
            _loginPanel.SetInteractable(true);
            SetUIInteractable(true);
        }
    }

    private async void OnRegisterButtonClicked()
    {
        var loginFlow = LoginFlow;
        if (loginFlow == null)
        {
            Debug.LogError($"[{nameof(MainMenuController)}] LoginFlowController not assigned.");
            return;
        }

        _loginPanel.SetInteractable(false);
        _loginPanel.SetStatus("Registering...");
        SetUIInteractable(false);

        try
        {
            LoginFlowResult result = await loginFlow.ExecuteRegisterAsync(
                _loginPanel.Username,
                _loginPanel.Password);
            if (this != null) HandleLoginFlowResult(result);
        }
        catch (Exception ex)
        {
            if (this == null) return;
            _loginPanel.SetStatus(ex.Message);
            _loginPanel.SetInteractable(true);
            SetUIInteractable(true);
        }
    }

    private void HandleLoginFlowResult(LoginFlowResult result)
    {
        SetUIInteractable(true);
        if (result.IsSuccess)
        {
            _loginPanel.SetStatus(string.Empty);
            UiPanelVisibility.SetVisible(_loginPanel.gameObject, false);
            UiPanelVisibility.SetVisible(menuPanel, true);
            SetStatus(string.Empty);
            RefreshSessionActions();
        }
        else if (result.Status == LoginFlowStatus.NeedsCharacterCreation)
        {
            UiPanelVisibility.SetVisible(_loginPanel.gameObject, false);

            if (_characterCreationPanel != null)
            {
                UiPanelVisibility.SetVisible(_characterCreationPanel.gameObject, true);
                _characterCreationPanel.SetStatus("Account created. Please choose a character name.");
                _characterCreationPanel.SetInteractable(true);
            }
            else
            {
                Debug.LogError($"[{nameof(MainMenuController)}] Needs character creation but no panel assigned!");
            }
        }
        else
        {
            _loginPanel.SetStatus(result.ErrorMessage);
            _loginPanel.SetInteractable(true);
        }
    }

    private async void OnCreateCharacterButtonClicked()
    {
        var loginFlow = LoginFlow;
        if (loginFlow == null) return;

        _characterCreationPanel.SetInteractable(false);
        _characterCreationPanel.SetStatus("Creating character...");
        SetUIInteractable(false);

        LoginFlowResult result;
        try
        {
            result = await loginFlow.CreateCharacterAsync(_characterCreationPanel.CharacterName);
        }
        catch (Exception ex)
        {
            if (this == null) return;
            _characterCreationPanel.SetStatus(ex.Message);
            _characterCreationPanel.SetInteractable(true);
            SetUIInteractable(true);
            return;
        }
        if (this == null) return;
        SetUIInteractable(true);

        if (result.IsSuccess)
        {
            _characterCreationPanel.SetStatus(string.Empty);
            UiPanelVisibility.SetVisible(_characterCreationPanel.gameObject, false);
            UiPanelVisibility.SetVisible(menuPanel, true);
            SetStatus(string.Empty);
            RefreshSessionActions();
        }
        else
        {
            _characterCreationPanel.SetStatus(result.ErrorMessage);
            _characterCreationPanel.SetInteractable(true);
        }
    }

    private void RefreshSessionActions()
    {
        var actions = new MainMenuSessionActions(
            ApplicationAuthContext.Instance?.IsAuthenticated == true, _isBusy);
        createRoomButton.gameObject.SetActive(actions.PlayVisible);
        createRoomButton.interactable = actions.PlayInteractable;
        if (_loginButton != null) _loginButton.gameObject.SetActive(actions.LoginVisible);
        if (_logoutButton != null) _logoutButton.gameObject.SetActive(actions.LogoutVisible);
        if (_loginButton != null) _loginButton.interactable = actions.SecondaryInteractable;
        if (_logoutButton != null) _logoutButton.interactable = actions.SecondaryInteractable;
        if (_settingsButton != null) _settingsButton.interactable = actions.SecondaryInteractable;
        if (_creditsButton != null) _creditsButton.interactable = actions.SecondaryInteractable;
        if (_exitButton != null) _exitButton.interactable = actions.SecondaryInteractable;
    }

    private void SetStatus(string message)
    {
        if (_statusText == null) return;
        _statusText.text = message;
        _statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private void OnSettingsButtonClicked()
    {
        if (!_isBusy) _settingsPanel?.Open();
    }

    private void OnCreditsButtonClicked()
    {
        if (!_isBusy) _creditsPanel?.Open();
    }

    private void OnExitButtonClicked()
    {
        if (!_isBusy) QuitAction();
    }

    private async void OnLogoutButtonClicked()
    {
        if (_isBusy) return;
        SetUIInteractable(false);
        SetStatus("Logging out...");
        try
        {
            var loginFlow = LoginFlow;
            if (loginFlow == null)
            {
                throw new InvalidOperationException("LoginFlowController is unavailable.");
            }
            await loginFlow.ExecuteLogoutAsync();
            if (this == null) return;
            SetStatus(string.Empty);
        }
        catch (Exception ex)
        {
            if (this == null) return;
            SetStatus($"Failed to log out: {ex.Message}");
        }
        finally
        {
            if (this != null) SetUIInteractable(true);
        }
    }

    private void RefreshConnectionButtons()
    {
        RefreshSessionActions();
        joinRoomButton.interactable = !_isBusy;
    }

    private void SetUIInteractable(bool interactable)
    {
        _isBusy = !interactable;
        roomCodeInput.interactable = interactable;
        RefreshConnectionButtons();
    }

    public async void CreateRoom()
    {
        if (_isBusy) return;
        if (ApplicationAuthContext.Instance?.IsAuthenticated != true)
        {
            ShowLoginPanel();
            return;
        }

        SetUIInteractable(false);
        SetStatus("Connecting to Town...");

        try
        {
            var coordinator = ConnectionCoordinator;
            if (coordinator == null)
            {
                throw new InvalidOperationException("Session connection coordinator is unavailable.");
            }

            SessionTransitionResult result =
                await coordinator.ConnectToTownAsync();

            if (result != SessionTransitionResult.Succeeded)
            {
                SetStatus($"Failed to enter Town: {result}.");
                UiPanelVisibility.SetVisible(menuPanel, true);
                SetUIInteractable(true);
            }
            else
            {
                _isBusy = false;
                SetStatus(string.Empty);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error entering Town: {ex.Message}");
            SetStatus($"Failed to enter Town: {ex.Message}");
            UiPanelVisibility.SetVisible(menuPanel, true);
            SetUIInteractable(true);
        }
    }

    private void ShowLoginPanel()
    {
        if (_isBusy) return;
        SetStatus(string.Empty);
        if (_loginPanel == null)
        {
            SetStatus("Login is unavailable.");
            Debug.LogError($"[{nameof(MainMenuController)}] Cannot continue without a LoginPanelView.");
            return;
        }

        UiPanelVisibility.SetVisible(menuPanel, false);
        _loginPanel.ClearFields();
        _loginPanel.SetStatus(string.Empty);
        _loginPanel.SetInteractable(true);
        UiPanelVisibility.SetVisible(_loginPanel.gameObject, true);
    }

    private void OnLoginBackButtonClicked()
    {
        if (_isBusy) return;
        _loginPanel.ClearFields();
        _loginPanel.SetStatus(string.Empty);
        UiPanelVisibility.SetVisible(_loginPanel.gameObject, false);
        UiPanelVisibility.SetVisible(menuPanel, true);
        RefreshSessionActions();
    }

    public async void JoinRoom()
    {
        if (string.IsNullOrEmpty(roomCodeInput.text))
        {
            SetStatus("Please enter a room code.");
            return;
        }

        if (roomCodeInput.text.Length < 6)
        {
            SetStatus("Invalid room code");
            return;
        }

        SetUIInteractable(false);
        SetStatus("Joining...");

        SessionTransitionResult result = SessionTransitionResult.ConnectionFailed;
        try
        {
            var coordinator = ConnectionCoordinator;
            if (coordinator == null)
            {
                throw new InvalidOperationException("Session connection coordinator is unavailable.");
            }

            result = await coordinator.StartDirectRaidForDevelopmentAsync(
                roomCodeInput.text,
                GameMode.Client);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error joining room: {ex.Message}");
            SetStatus($"Failed to join room: {ex.Message}");
        }

        if (result == SessionTransitionResult.Succeeded)
        {
            _isBusy = false;
            SetStatus(string.Empty);
        }
        else
        {
            SetUIInteractable(true);
            if (_statusText != null && _statusText.text == "Joining...")
            {
                SetStatus("Failed to join room: Session is closed, full, or does not exist.");
            }
        }
    }
}
