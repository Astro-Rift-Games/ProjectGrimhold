using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Toggles the Town pause panel when Escape is not consumed by an open panel. While a local
/// <see cref="PlayerInputReader"/> exists, Escape reaches this presenter only through
/// <see cref="PlayerInputReader.MenuToggleRequested"/>, which fires after every
/// <see cref="PlayerInputReader.InventoryCloseRequested"/> handler declined the press. Without a local
/// reader (for example before the player spawns) it falls back to reading the Escape key directly.
/// Delegates the logout sequence to <see cref="SessionConnectionCoordinator"/> and
/// <see cref="LoginFlowController"/>, both of which are DontDestroyOnLoad singletons.
/// This component lives in the Lobby-Town scene and is destroyed when the scene unloads.
/// Time.timeScale is never modified.
/// </summary>
[RequireComponent(typeof(TownPauseMenuView))]
[DisallowMultipleComponent]
public sealed class TownPauseMenuPresenter : MonoBehaviour
{
    private const float ContextLookupIntervalSeconds = 0.5f;

    private TownPauseMenuView _view;
    private LocalInputContext _inputContext;
    private PlayerInputReader _reader;
    private float _nextContextLookupTime;
    private bool _logoutInProgress;

    private void Awake()
    {
        _view = GetComponent<TownPauseMenuView>();
    }

    private void OnEnable()
    {
        _view.ResumeClicked += OnResumeClicked;
        _view.LogoutClicked += OnLogoutClicked;
        ResolveInputContext();
    }

    private void OnDisable()
    {
        _view.ResumeClicked -= OnResumeClicked;
        _view.LogoutClicked -= OnLogoutClicked;
        ReleaseInputContext();
    }

    private void Update()
    {
        if (_inputContext == null)
        {
            ReleaseInputContext();
            if (Time.unscaledTime >= _nextContextLookupTime)
            {
                _nextContextLookupTime = Time.unscaledTime + ContextLookupIntervalSeconds;
                ResolveInputContext();
            }
        }

        if (_reader == null && Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    private void ResolveInputContext()
    {
        if (_inputContext != null)
        {
            return;
        }

        _inputContext = FindAnyObjectByType<LocalInputContext>();
        if (_inputContext == null)
        {
            return;
        }

        _inputContext.ReaderChanged += OnReaderChanged;
        OnReaderChanged(_inputContext.Reader);
    }

    private void ReleaseInputContext()
    {
        if (_inputContext != null)
        {
            _inputContext.ReaderChanged -= OnReaderChanged;
        }

        _inputContext = null;
        OnReaderChanged(null);
    }

    private void OnReaderChanged(PlayerInputReader reader)
    {
        if (_reader == reader)
        {
            return;
        }

        if (_reader != null)
        {
            _reader.MenuToggleRequested -= TogglePause;
        }

        _reader = reader;
        if (_reader != null)
        {
            _reader.MenuToggleRequested += TogglePause;
        }
    }

    private void TogglePause()
    {
        if (_logoutInProgress)
        {
            return;
        }

        if (_view.IsVisible)
            _view.Hide();
        else
            _view.Show();
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
