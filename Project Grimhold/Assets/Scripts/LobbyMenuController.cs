using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyMenuController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI sessionCodeText;
    [SerializeField] private Button _startGameButton;
    [SerializeField] private GameObject _statusText;
    [SerializeField] private GameObject _stashPanel;

    [Header("Scene")]
    [SerializeField] private string gameplayScene = "Gameplay";

    private NetworkRunner _runner;
    private FusionSessionLauncher _launcher;

    private void OnEnable()
    {
        _startGameButton.onClick.AddListener(StartGame);
    }

    private void OnDisable()
    {
        _startGameButton.onClick.RemoveListener(StartGame);
    }

    public void Initialize(NetworkRunner runner, FusionSessionLauncher launcher)
    {
        _runner = runner;
        _launcher = launcher;

        sessionCodeText.text = _runner.SessionInfo.Name;

        _startGameButton.gameObject.SetActive(_runner.IsServer);
        _statusText.gameObject.SetActive(!_runner.IsServer);
    }

    public void RefreshSessionCode()
    {
        if (_runner == null || sessionCodeText == null)
            return;

        sessionCodeText.text = _runner.SessionInfo.Name;
    }

    public void StartGame()
    {
        if (_runner == null || !_runner.IsServer)
            return;

        // Prevent multiple simultaneous clicks by disabling the button temporarily
        _startGameButton.interactable = false;

        var matchController = _launcher != null ? _launcher.MatchController : null;
        if (matchController != null)
        {
            if (!matchController.TryStartRaid())
            {
                Debug.LogError("[LobbyMenuController] Failed to start raid.");
                _startGameButton.interactable = true;
            }
        }
        else
        {
            Debug.LogError("[LobbyMenuController] NetworkMatchController instance not found on launcher!");
            _startGameButton.interactable = true;
        }
    }

    /// <summary>
    /// Toggles the active state of the stash UI panel.
    /// </summary>
    public void ToggleStash()
    {
        if (_stashPanel != null)
        {
            _stashPanel.SetActive(!_stashPanel.activeSelf);
        }
    }
}
