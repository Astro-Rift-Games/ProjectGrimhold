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

        RefreshConnectionButtons();
    }

    private void OnDisable()
    {
        createRoomButton.onClick.RemoveListener(CreateRoom);
        joinRoomButton.onClick.RemoveListener(JoinRoom);
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
