using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the login panel UI state. Provides read access to user input and write access
/// to status text and interactability. Contains no login logic.
/// </summary>
public sealed class LoginPanelView : MonoBehaviour
{
    [SerializeField] private TMP_InputField _usernameInput;
    [SerializeField] private TMP_InputField _passwordInput;
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _registerButton;
    [SerializeField] private TextMeshProUGUI _statusText;

    public string Username => _usernameInput.text.Trim();
    public string Password => _passwordInput.text;

    public void SetInteractable(bool interactable)
    {
        _usernameInput.interactable = interactable;
        _passwordInput.interactable = interactable;
        _loginButton.interactable = interactable;
        if (_registerButton != null) _registerButton.interactable = interactable;
    }

    public void ClearFields()
    {
        _usernameInput.text = string.Empty;
        _passwordInput.text = string.Empty;
    }

    public void SetStatus(string message)
    {
        if (_statusText != null)
            _statusText.text = message;
    }

    public void AddLoginListener(UnityEngine.Events.UnityAction action)
        => _loginButton.onClick.AddListener(action);

    public void RemoveLoginListener(UnityEngine.Events.UnityAction action)
        => _loginButton.onClick.RemoveListener(action);

    public void AddRegisterListener(UnityEngine.Events.UnityAction action)
    {
        if (_registerButton != null) _registerButton.onClick.AddListener(action);
    }

    public void RemoveRegisterListener(UnityEngine.Events.UnityAction action)
    {
        if (_registerButton != null) _registerButton.onClick.RemoveListener(action);
    }
}
