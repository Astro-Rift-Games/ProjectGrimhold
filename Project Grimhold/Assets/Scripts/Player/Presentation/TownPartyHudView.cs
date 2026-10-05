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

    private bool _owned;
    private bool _partyActive;

    public event Action LeaveRequested;

    private void Awake() => _leaveButton?.onClick.AddListener(OnLeaveRequested);
    private void OnDestroy() => _leaveButton?.onClick.RemoveListener(OnLeaveRequested);

    public void SetOwned(bool owned)
    {
        _owned = owned;
        if (!owned) _partyActive = false;
        RefreshVisibility();
    }

    /// <summary>Presents a party. The HUD is only visible while the party has a companion.</summary>
    public void Present(in TownPartyPresentation presentation)
    {
        if (_localNameText != null) _localNameText.text = presentation.LocalDisplayName;
        if (_companionNameText != null)
        {
            _companionNameText.text = presentation.CompanionDisplayName;
            _companionNameText.gameObject.SetActive(presentation.HasCompanion);
        }
        if (_leaveButton != null) _leaveButton.gameObject.SetActive(presentation.HasCompanion);
        _partyActive = presentation.HasCompanion;
        RefreshVisibility();
    }

    /// <summary>Hides the HUD when no party is observable.</summary>
    public void ClearParty()
    {
        _partyActive = false;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (_root != null) _root.SetActive(_owned && _partyActive);
    }

    private void OnLeaveRequested() => LeaveRequested?.Invoke();
}
