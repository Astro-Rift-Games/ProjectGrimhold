using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Passive content of the Town menu Options tab. It currently only offers session exit actions;
/// sound, graphics and other settings are added here later.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownOptionsView : MonoBehaviour
{
    [SerializeField] private Button _logoutButton;
    [SerializeField] private Button _exitButton;

    public event Action LogoutRequested;
    public event Action ExitRequested;

    private void Awake()
    {
        _logoutButton?.onClick.AddListener(OnLogoutClicked);
        _exitButton?.onClick.AddListener(OnExitClicked);
    }

    private void OnDestroy()
    {
        _logoutButton?.onClick.RemoveListener(OnLogoutClicked);
        _exitButton?.onClick.RemoveListener(OnExitClicked);
    }

    public void Open() => gameObject.SetActive(true);

    public void Close() => gameObject.SetActive(false);

    /// <summary>Enables or disables both actions, for example while logout is running.</summary>
    public void SetInteractable(bool interactable)
    {
        if (_logoutButton != null) _logoutButton.interactable = interactable;
        if (_exitButton != null) _exitButton.interactable = interactable;
    }

    private void OnLogoutClicked() => LogoutRequested?.Invoke();

    private void OnExitClicked() => ExitRequested?.Invoke();
}
