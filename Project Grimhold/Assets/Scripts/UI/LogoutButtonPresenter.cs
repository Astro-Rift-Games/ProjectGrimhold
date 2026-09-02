using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LogoutButtonPresenter : MonoBehaviour
{
    [SerializeField] private Button _logoutButton;
    [SerializeField] private LoginFlowController _loginFlowController;
    [SerializeField] private SessionConnectionCoordinator _coordinator;

    private void Awake()
    {
        if (_logoutButton == null)
        {
            _logoutButton = GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        if (_logoutButton != null)
        {
            _logoutButton.onClick.AddListener(OnLogoutClicked);
        }
    }

    private void OnDisable()
    {
        if (_logoutButton != null)
        {
            _logoutButton.onClick.RemoveListener(OnLogoutClicked);
        }
    }

    private async void OnLogoutClicked()
    {
        if (_logoutButton != null)
        {
            _logoutButton.interactable = false;
        }

        // 1. Attempt to return to main menu (disconnects Town runner)
        if (_coordinator != null)
        {
            var result = await _coordinator.ReturnToMainMenuAsync();
            if (result != SessionTransitionResult.Succeeded)
            {
                Debug.LogWarning($"[{nameof(LogoutButtonPresenter)}] Failed to cleanly return to main menu: {result}");
            }
        }
        else
        {
            // Fallback for when the coordinator reference is not set, try to find it.
            _coordinator = FindAnyObjectByType<SessionConnectionCoordinator>();
            if (_coordinator != null)
            {
                await _coordinator.ReturnToMainMenuAsync();
            }
        }

        // 2. Clear authentication and stash contexts
        if (_loginFlowController == null)
        {
            _loginFlowController = FindAnyObjectByType<LoginFlowController>();
        }

        if (_loginFlowController != null)
        {
            await _loginFlowController.ExecuteLogoutAsync();
        }
        else
        {
            Debug.LogError($"[{nameof(LogoutButtonPresenter)}] LoginFlowController is not assigned or found.");
        }

        // 3. Ensure we load the MainMenu scene if coordinator didn't do it automatically
        SceneManager.LoadScene("MainMenu");
    }
}
