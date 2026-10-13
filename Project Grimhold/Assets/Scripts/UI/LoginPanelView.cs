using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
    [SerializeField] private Button _backButton;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private InputActionReference _cancelAction;

    private InputAction _subscribedCancelAction;
    private bool _enabledCancelAction;
    private bool _missingCancelActionLogged;

    public string Username => _usernameInput.text.Trim();
    public string Password => _passwordInput.text;

    private void OnEnable()
    {
        InputAction action = _cancelAction != null ? _cancelAction.action : null;
        if (action == null)
        {
            if (!_missingCancelActionLogged)
            {
                _missingCancelActionLogged = true;
                Debug.LogError($"[{nameof(LoginPanelView)}] No cancel action assigned; Escape cannot trigger Back.", this);
            }
            return;
        }

        action.performed += OnCancelPerformed;
        _subscribedCancelAction = action;

        if (!action.enabled)
        {
            action.Enable();
            _enabledCancelAction = true;
        }
    }

    private void OnDisable()
    {
        if (_subscribedCancelAction == null) return;

        _subscribedCancelAction.performed -= OnCancelPerformed;
        if (_enabledCancelAction)
        {
            _subscribedCancelAction.Disable();
        }

        _subscribedCancelAction = null;
        _enabledCancelAction = false;
    }

    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        if (_backButton == null || !_backButton.isActiveAndEnabled || !_backButton.interactable) return;

        _backButton.onClick.Invoke();
    }

    public void SetInteractable(bool interactable)
    {
        _usernameInput.interactable = interactable;
        _passwordInput.interactable = interactable;
        _loginButton.interactable = interactable;
        if (_registerButton != null) _registerButton.interactable = interactable;
        if (_backButton != null) _backButton.interactable = interactable;
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

    public void AddBackListener(UnityEngine.Events.UnityAction action)
    {
        if (_backButton != null) _backButton.onClick.AddListener(action);
    }

    public void RemoveBackListener(UnityEngine.Events.UnityAction action)
    {
        if (_backButton != null) _backButton.onClick.RemoveListener(action);
    }
}
