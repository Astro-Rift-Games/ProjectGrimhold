using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Content of the Town menu Options tab, offering shared settings and session exit actions.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownOptionsView : MonoBehaviour
{
    [SerializeField] private Button _logoutButton;
    [SerializeField] private Button _exitButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private SettingsPanelView _settingsPanel;

    public event Action LogoutRequested;
    public event Action ExitRequested;

    private void Awake()
    {
        _logoutButton?.onClick.AddListener(OnLogoutClicked);
        _exitButton?.onClick.AddListener(OnExitClicked);
        _settingsButton?.onClick.AddListener(OnSettingsClicked);
    }

    private void OnDestroy()
    {
        _logoutButton?.onClick.RemoveListener(OnLogoutClicked);
        _exitButton?.onClick.RemoveListener(OnExitClicked);
        _settingsButton?.onClick.RemoveListener(OnSettingsClicked);
    }

    public void Open() => UiPanelVisibility.SetVisible(gameObject, true);

    public void Close()
    {
        if (_settingsPanel != null && _settingsPanel.IsOpen) _settingsPanel.Close();
        UiPanelVisibility.SetVisible(gameObject, false);
    }

    /// <summary>Enables or disables all actions, for example while logout is running.</summary>
    public void SetInteractable(bool interactable)
    {
        if (_logoutButton != null) _logoutButton.interactable = interactable;
        if (_exitButton != null) _exitButton.interactable = interactable;
        if (_settingsButton != null) _settingsButton.interactable = interactable;
    }

    private void OnLogoutClicked() => LogoutRequested?.Invoke();

    private void OnExitClicked() => ExitRequested?.Invoke();

    private void OnSettingsClicked() => _settingsPanel?.Open();
}
