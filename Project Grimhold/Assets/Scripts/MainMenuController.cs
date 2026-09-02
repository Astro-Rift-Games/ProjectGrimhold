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

        TMP_Text createButtonLabel = createRoomButton.GetComponentInChildren<TMP_Text>(true);
        if (createButtonLabel != null)
        {
            createButtonLabel.text = "Enter Town";
        }

        // "Enter Town" is locked until the login flow completes successfully.
        createRoomButton.interactable = false;

        if (_characterCreationPanel != null)
        {
            _characterCreationPanel.gameObject.SetActive(false);
            _characterCreationPanel.AddCreateListener(OnCreateCharacterButtonClicked);
        }

        if (_loginPanel != null)
        {
            _loginPanel.gameObject.SetActive(true);
            _loginPanel.SetStatus(string.Empty);
            _loginPanel.ClearFields();
            _loginPanel.AddLoginListener(OnLoginButtonClicked);
            _loginPanel.AddRegisterListener(OnRegisterButtonClicked);
        }
        else
        {
            // No login panel assigned: allow access for Editor development workflows.
            Debug.LogWarning($"[{nameof(MainMenuController)}] No LoginPanelView assigned. Enter Town enabled without authentication.");
            createRoomButton.interactable = true;
        }
    }

    private void OnDisable()
    {
        createRoomButton.onClick.RemoveListener(CreateRoom);
        joinRoomButton.onClick.RemoveListener(JoinRoom);

        if (_loginPanel != null)
        {
            _loginPanel.RemoveLoginListener(OnLoginButtonClicked);
            _loginPanel.RemoveRegisterListener(OnRegisterButtonClicked);
        }

        if (_characterCreationPanel != null)
        {
            _characterCreationPanel.RemoveCreateListener(OnCreateCharacterButtonClicked);
        }
    }

    private async void OnLoginButtonClicked()
    {
        if (_loginFlowController == null)
        {
            Debug.LogError($"[{nameof(MainMenuController)}] LoginFlowController not assigned.");
            return;
        }

        _loginPanel.SetInteractable(false);
        _loginPanel.SetStatus("Logging in...");

        LoginFlowResult result = await _loginFlowController.ExecuteLoginAsync(
            _loginPanel.Username,
            _loginPanel.Password);

        HandleLoginFlowResult(result);
    }

    private async void OnRegisterButtonClicked()
    {
        if (_loginFlowController == null)
        {
            Debug.LogError($"[{nameof(MainMenuController)}] LoginFlowController not assigned.");
            return;
        }

        _loginPanel.SetInteractable(false);
        _loginPanel.SetStatus("Registering...");

        LoginFlowResult result = await _loginFlowController.ExecuteRegisterAsync(
            _loginPanel.Username,
            _loginPanel.Password);

        HandleLoginFlowResult(result);
    }

    private void HandleLoginFlowResult(LoginFlowResult result)
    {
        if (result.IsSuccess)
        {
            _loginPanel.SetStatus("Success.");
            _loginPanel.gameObject.SetActive(false);
            createRoomButton.interactable = true;
        }
        else if (result.Status == LoginFlowStatus.NeedsCharacterCreation)
        {
            _loginPanel.gameObject.SetActive(false);

            if (_characterCreationPanel != null)
            {
                _characterCreationPanel.gameObject.SetActive(true);
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
        if (_loginFlowController == null) return;

        _characterCreationPanel.SetInteractable(false);
        _characterCreationPanel.SetStatus("Creating character...");

        LoginFlowResult result = await _loginFlowController.CreateCharacterAsync(_characterCreationPanel.CharacterName);

        if (result.IsSuccess)
        {
            _characterCreationPanel.SetStatus("Success.");
            _characterCreationPanel.gameObject.SetActive(false);
            createRoomButton.interactable = true;
        }
        else
        {
            _characterCreationPanel.SetStatus(result.ErrorMessage);
            _characterCreationPanel.SetInteractable(true);
        }
    }

    private void RefreshConnectionButtons()
    {
        createRoomButton.interactable = true;
        joinRoomButton.interactable = true;
    }

    private void SetUIInteractable(bool interactable)
    {
        roomCodeInput.interactable = interactable;

        if (interactable)
        {
            RefreshConnectionButtons();
        }
        else
        {
            createRoomButton.interactable = false;
            joinRoomButton.interactable = false;
        }
    }

    public async void CreateRoom()
    {
        SetUIInteractable(false);
        _statusText.text = "Connecting to Town...";

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
                _statusText.text = $"Failed to enter Town: {result}.";
                SetUIInteractable(true);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error entering Town: {ex.Message}");
            _statusText.text = $"Failed to enter Town: {ex.Message}";
            SetUIInteractable(true);
        }
    }

    public async void JoinRoom()
    {
        if (string.IsNullOrEmpty(roomCodeInput.text))
        {
            _statusText.text = "Please enter a room code.";
            return;
        }

        if (roomCodeInput.text.Length < 6)
        {
            _statusText.text = "Invalid room code";
            return;
        }

        SetUIInteractable(false);
        _statusText.text = "Joining...";

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
            _statusText.text = $"Failed to join room: {ex.Message}";
        }

        if (result == SessionTransitionResult.Succeeded)
        {
            _statusText.text = "Connected through the direct development route.";
        }
        else
        {
            SetUIInteractable(true);
            if (_statusText.text == "Joining...")
            {
                _statusText.text = "Failed to join room: Session is closed, full, or does not exist.";
            }
        }
    }
}
