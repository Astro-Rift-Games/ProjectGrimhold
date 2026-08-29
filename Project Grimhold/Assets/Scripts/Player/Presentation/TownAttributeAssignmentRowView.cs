using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored view for one known character attribute row.</summary>
[DisallowMultipleComponent]
public sealed class TownAttributeAssignmentRowView : MonoBehaviour
{
    [SerializeField] private CharacterAttribute _attribute;
    [SerializeField] private TMP_Text _labelText;
    [SerializeField] private TMP_Text _valueText;
    [SerializeField] private Button _addButton;

    public CharacterAttribute Attribute => _attribute;
    public TMP_Text LabelText => _labelText;
    public TMP_Text ValueText => _valueText;
    public Button AddButton => _addButton;

    public event Action<CharacterAttribute> AssignmentRequested;

    private void Awake()
    {
        if (_addButton != null)
        {
            _addButton.onClick.AddListener(RequestAssignment);
        }
    }

    private void OnDestroy()
    {
        if (_addButton != null)
        {
            _addButton.onClick.RemoveListener(RequestAssignment);
        }
    }

    public void Present(int value, bool canAssign)
    {
        _valueText.text = value.ToString();
        _addButton.interactable = canAssign;
    }

    public void PresentUnavailable()
    {
        _valueText.text = "—";
        _addButton.interactable = false;
    }

    private void RequestAssignment() => AssignmentRequested?.Invoke(_attribute);
}
