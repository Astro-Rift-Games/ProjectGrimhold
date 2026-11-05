using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Authored screen-local irreversible gem confirmation. It owns no inventory state.</summary>
[DisallowMultipleComponent]
public sealed class GemInstallConfirmationView : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _message;
    [SerializeField] private UnityEngine.UI.Button _confirmButton;
    [SerializeField] private UnityEngine.UI.Button _cancelButton;
    public event Action ConfirmRequested;
    public event Action CancelRequested;
    public bool IsOpen => _root != null && _root.activeSelf;
    private void Awake()
    {
        _confirmButton?.onClick.AddListener(OnConfirm);
        _cancelButton?.onClick.AddListener(OnCancel);
        _root?.SetActive(false);
    }
    private void OnDestroy()
    {
        _confirmButton?.onClick.RemoveListener(OnConfirm);
        _cancelButton?.onClick.RemoveListener(OnCancel);
    }
    private void OnConfirm() => ConfirmRequested?.Invoke();
    private void OnCancel() => CancelRequested?.Invoke();
    public void Present(TownGemInstallSession session)
    {
        bool visible = session != null && session.IsConfirming;
        if (_root == null || _message == null || _confirmButton == null || _cancelButton == null) return;
        bool wasVisible = IsOpen;
        _message.text = visible ? session.Confirmation : string.Empty;
        _root.SetActive(visible);
        if (visible && !wasVisible && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_cancelButton.gameObject);
    }
}
