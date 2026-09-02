using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pure UI view for the Town pause menu.
/// Exposes Show/Hide and wires button callbacks to the Presenter via UnityEvents.
/// Does not contain any gameplay or session logic.
/// </summary>
public sealed class TownPauseMenuView : MonoBehaviour
{
    [SerializeField] private GameObject _rootPanel;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _logoutButton;

    public event System.Action ResumeClicked;
    public event System.Action LogoutClicked;

    private void OnEnable()
    {
        _resumeButton?.onClick.AddListener(OnResumeClicked);
        _logoutButton?.onClick.AddListener(OnLogoutClicked);
    }

    private void OnDisable()
    {
        _resumeButton?.onClick.RemoveListener(OnResumeClicked);
        _logoutButton?.onClick.RemoveListener(OnLogoutClicked);
    }

    public void Show()
    {
        _rootPanel.SetActive(true);
    }

    public void Hide()
    {
        _rootPanel.SetActive(false);
    }

    public bool IsVisible => _rootPanel != null && _rootPanel.activeSelf;

    /// <summary>
    /// Disables both buttons while the logout operation is in progress.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (_resumeButton != null) _resumeButton.interactable = interactable;
        if (_logoutButton != null) _logoutButton.interactable = interactable;
    }

    private void OnResumeClicked() => ResumeClicked?.Invoke();
    private void OnLogoutClicked() => LogoutClicked?.Invoke();
}
