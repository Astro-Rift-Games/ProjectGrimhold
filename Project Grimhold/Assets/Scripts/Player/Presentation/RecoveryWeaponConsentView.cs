using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Preauthored modal for a new free weapon only. It has no timer or inventory authority.</summary>
[DisallowMultipleComponent]
public sealed class RecoveryWeaponConsentView : MonoBehaviour
{
    [SerializeField] private SessionConnectionCoordinator _coordinator;
    [SerializeField] private GameObject _root;
    [SerializeField] private Button _acceptButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private TMP_Text _message;
    private Action<bool> _completion;

    public void BindCoordinator(SessionConnectionCoordinator coordinator)
    {
        if (_coordinator == coordinator) return;
        _coordinator?.UnbindRecoveryWeaponConsentView(this);
        _coordinator = coordinator;
        if (isActiveAndEnabled) _coordinator?.BindRecoveryWeaponConsentView(this);
    }

    private void OnEnable()
    {
        _acceptButton?.onClick.AddListener(Accept);
        _cancelButton?.onClick.AddListener(Cancel);
        _coordinator?.BindRecoveryWeaponConsentView(this);
        Close();
    }

    private void OnDisable()
    {
        _acceptButton?.onClick.RemoveListener(Accept);
        _cancelButton?.onClick.RemoveListener(Cancel);
        _coordinator?.UnbindRecoveryWeaponConsentView(this);
        Complete(false);
    }

    public bool TryOpen(Action<bool> completion)
    {
        if (_root == null || _root == gameObject || _acceptButton == null || _cancelButton == null || _message == null || _completion != null)
            return false;
        _completion = completion;
        _message.text = "You have no prepared weapon. Accept a free recovery weapon and continue to the Raid?";
        _root.SetActive(true);
        return true;
    }

    public void Close()
    {
        _completion = null;
        if (_root != null && _root != gameObject) _root.SetActive(false);
    }

    private void Accept() => Complete(true);
    private void Cancel() => Complete(false);
    private void Complete(bool accepted)
    {
        Action<bool> completion = _completion;
        Close();
        completion?.Invoke(accepted);
    }
}
