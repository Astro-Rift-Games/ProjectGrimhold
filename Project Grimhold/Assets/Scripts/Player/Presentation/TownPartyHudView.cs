using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TownPartyHudView : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _localNameText;
    [SerializeField] private TMP_Text _companionNameText;
    [SerializeField] private Button _leaveButton;

    public event Action LeaveRequested;

    private void Awake() => _leaveButton?.onClick.AddListener(OnLeaveRequested);
    private void OnDestroy() => _leaveButton?.onClick.RemoveListener(OnLeaveRequested);

    public void SetOwned(bool owned)
    {
        if (_root != null) _root.SetActive(owned);
    }

    public void Present(in TownPartyPresentation presentation)
    {
        if (_localNameText != null) _localNameText.text = presentation.LocalDisplayName;
        if (_companionNameText != null)
        {
            _companionNameText.text = presentation.CompanionDisplayName;
            _companionNameText.gameObject.SetActive(presentation.HasCompanion);
        }
        if (_leaveButton != null) _leaveButton.gameObject.SetActive(presentation.HasCompanion);
    }

    private void OnLeaveRequested() => LeaveRequested?.Invoke();
}
