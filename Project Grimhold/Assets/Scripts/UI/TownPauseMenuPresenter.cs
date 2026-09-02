using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Listens for the ESC key each frame and toggles the Town pause panel.
/// Delegates the logout sequence to <see cref="SessionConnectionCoordinator"/> and
/// <see cref="LoginFlowController"/>, both of which are DontDestroyOnLoad singletons.
/// This component lives in the Lobby-Town scene and is destroyed when the scene unloads.
/// Time.timeScale is never modified.
/// </summary>
[RequireComponent(typeof(TownPauseMenuView))]
[DisallowMultipleComponent]
public sealed class TownPauseMenuPresenter : MonoBehaviour
{
    private TownPauseMenuView _view;
    private bool _logoutInProgress;

    private void Awake()
    {
        _view = GetComponent<TownPauseMenuView>();
    }

    private void OnEnable()
    {
        _view.ResumeClicked += OnResumeClicked;
        _view.LogoutClicked += OnLogoutClicked;
    }

    private void OnDisable()
    {
        _view.ResumeClicked -= OnResumeClicked;
        _view.LogoutClicked -= OnLogoutClicked;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !_logoutInProgress)
        {
            if (_view.IsVisible)
                _view.Hide();
            else
                _view.Show();
        }
    }

    private void OnResumeClicked()
    {
        _view.Hide();
    }

    private async void OnLogoutClicked()
    {
        if (_logoutInProgress) return;
        _logoutInProgress = true;
        _view.SetInteractable(false);

        // 1. Shut down the Town runner and return the coordinator to MainMenu state.
        var coordinator = SessionConnectionCoordinator.Instance
            ?? FindAnyObjectByType<SessionConnectionCoordinator>();

        if (coordinator != null)
        {
            var result = await coordinator.ReturnToMainMenuAsync();
            if (result != SessionTransitionResult.Succeeded)
            {
                Debug.LogWarning(
                    $"[{nameof(TownPauseMenuPresenter)}] ReturnToMainMenuAsync result: {result}. Proceeding with logout.");
            }
        }
        else
        {
            Debug.LogError($"[{nameof(TownPauseMenuPresenter)}] SessionConnectionCoordinator not found.");
        }

        // 2. Clear auth state and stash contexts.
        var loginFlow = LoginFlowController.Instance
            ?? FindAnyObjectByType<LoginFlowController>();

        if (loginFlow != null)
        {
            await loginFlow.ExecuteLogoutAsync();
        }
        else
        {
            Debug.LogError($"[{nameof(TownPauseMenuPresenter)}] LoginFlowController not found.");
        }

        // 3. Load MainMenu. This presenter is destroyed when Lobby-Town unloads.
        SceneManager.LoadScene("MainMenu");
    }
}
