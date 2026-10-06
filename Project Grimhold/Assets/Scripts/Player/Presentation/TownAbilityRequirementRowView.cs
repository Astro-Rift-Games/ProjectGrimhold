using TMPro;
using UnityEngine;

/// <summary>One requirement line of the "Requirement Check" panel.</summary>
[DisallowMultipleComponent]
public sealed class TownAbilityRequirementRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _labelText;
    [SerializeField] private TMP_Text _valueText;
    [SerializeField] private Color _metColor = new(0.45f, 0.78f, 0.4f, 1f);
    [SerializeField] private Color _unmetColor = new(0.85f, 0.3f, 0.25f, 1f);

    public TMP_Text StatusText => _statusText;
    public TMP_Text LabelText => _labelText;
    public TMP_Text ValueText => _valueText;

    public void Present(in TownAbilityRequirementCheck check, string abilityName)
    {
        Color color = check.IsMet ? _metColor : _unmetColor;
        _statusText.text = check.IsMet ? "OK" : "X";
        _statusText.color = color;
        _labelText.text = TownAbilityText.RequirementLabel(check, abilityName);
        _valueText.text = TownAbilityText.CurrentOverRequired(check);
        _valueText.color = color;
    }
}
