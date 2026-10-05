using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plugs the Options tab into the Town player menu and runs its actions: log out of the session and
/// exit the game. Logout delegates to <see cref="SessionConnectionCoordinator"/> and
/// <see cref="LoginFlowController"/>, both DontDestroyOnLoad singletons. Time.timeScale is never modified.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownOptionsPresenter : MonoBehaviour
{
    [SerializeField] private TownOptionsView _viewPrefab;

    private TownPlayerMenuPresenter _menu;
    private TownOptionsView _view;
    private bool _logoutInProgress;

    /// <summary>Runs the logout sequence. Replaceable so tests never touch the real session.</summary>
    public Func<Task> LogoutAction { get; set; }

    /// <summary>Quits the application. Replaceable so tests never quit the Editor.</summary>
    public Action QuitAction { get; set; }

    private void Awake()
    {
        LogoutAction ??= RunLogoutAsync;
        QuitAction ??= QuitApplication;
    }

    /// <summary>Creates the Options content and registers it as a tab of the menu.</summary>
    public void Register(TownPlayerMenuPresenter menu)
    {
        Unregister();
        if (menu == null)
        {
            return;
        }

        if (_viewPrefab == null)
        {
            Debug.LogError($"{nameof(TownOptionsPresenter)} is missing its serialized view prefab.", this);
            return;
        }

        _menu = menu;
        _view = Instantiate(_viewPrefab, transform, false);
        _view.name = _viewPrefab.name;
        _view.Close();
        _view.LogoutRequested += OnLogoutRequested;
        _view.ExitRequested += OnExitRequested;
        _menu.RegisterTab(new TownMenuTabRegistration(
            TownMenuTabIds.Options,
            "Options",
            (RectTransform)_view.transform,
            _view.Open,
            _view.Close));
    }

    public void Unregister()
    {
        if (_menu != null)
        {
            _menu.UnregisterTab(TownMenuTabIds.Options);
        }

        _menu = null;
        if (_view == null)
        {
            return;
        }

        _view.LogoutRequested -= OnLogoutRequested;
        _view.ExitRequested -= OnExitRequested;
        Destroy(_view.gameObject);
        _view = null;
    }

    private void OnDestroy() => Unregister();

    private async void OnLogoutRequested()
    {
        if (_logoutInProgress)
        {
            return;
        }

        _logoutInProgress = true;
        _view.SetInteractable(false);
        try
        {
            await LogoutAction();
        }
        finally
        {
            _logoutInProgress = false;
            if (_view != null)
            {
                _view.SetInteractable(true);
            }
        }
    }

    private void OnExitRequested()
    {
        if (!_logoutInProgress)
        {
            QuitAction();
        }
    }

    private static async Task RunLogoutAsync()
    {
        // 1. Shut down the Town runner and return the coordinator to MainMenu state.
        var coordinator = SessionConnectionCoordinator.Instance
            ?? FindAnyObjectByType<SessionConnectionCoordinator>();

        if (coordinator != null)
        {
            var result = await coordinator.ReturnToMainMenuAsync();
            if (result != SessionTransitionResult.Succeeded)
            {
                Debug.LogWarning(
                    $"[{nameof(TownOptionsPresenter)}] ReturnToMainMenuAsync result: {result}. Proceeding with logout.");
            }
        }
        else
        {
            Debug.LogError($"[{nameof(TownOptionsPresenter)}] SessionConnectionCoordinator not found.");
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
            Debug.LogError($"[{nameof(TownOptionsPresenter)}] LoginFlowController not found.");
        }

        // 3. Load MainMenu. This presenter is destroyed when Lobby-Town unloads.
        SceneManager.LoadScene("MainMenu");
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
