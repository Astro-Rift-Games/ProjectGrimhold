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
    private LoginFlowController _loginFlowController;

    private void OnEnable()
    {
        createRoomButton.onClick.AddListener(CreateRoom);
        joinRoomButton.onClick.AddListener(JoinRoom);

        if (_connectionCoordinator == null)
        {
            _connectionCoordinator = SessionConnectionCoordinator.Instance;
        }

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

        if (_loginPanel != null)
        {
            _loginPanel.gameObject.SetActive(true);
            _loginPanel.SetStatus(string.Empty);
            _loginPanel.AddLoginListener(OnLoginButtonClicked);
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

        if (result.IsSuccess)
        {
            _loginPanel.SetStatus("Login successful.");
            _loginPanel.gameObject.SetActive(false);
            createRoomButton.interactable = true;
        }
        else
        {
            _loginPanel.SetStatus(result.ErrorMessage);
            _loginPanel.SetInteractable(true);
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
            if (_connectionCoordinator == null)
            {
                throw new InvalidOperationException("Session connection coordinator is unavailable.");
            }

            SessionTransitionResult result =
                await _connectionCoordinator.ConnectToTownAsync();

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
            if (_connectionCoordinator == null)
            {
                throw new InvalidOperationException("Session connection coordinator is unavailable.");
            }

            result = await _connectionCoordinator.StartDirectRaidForDevelopmentAsync(
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
