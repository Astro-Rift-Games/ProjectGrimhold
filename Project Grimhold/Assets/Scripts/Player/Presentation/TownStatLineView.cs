using TMPro;
using UnityEngine;

/// <summary>One row of the Town statistics column: label, value and an optional detail.</summary>
[DisallowMultipleComponent]
public sealed class TownStatLineView : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private TMP_Text _value;
    [SerializeField] private TMP_Text _detail;

    public string Label => _label != null ? _label.text : string.Empty;
    public string Value => _value != null ? _value.text : string.Empty;
    public string Detail => _detail != null ? _detail.text : string.Empty;

    public void Present(in TownStatLine line)
    {
        SetText(_label, line.Label);
        SetText(_value, line.Value);
        SetText(_detail, line.Detail);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text == null)
        {
            return;
        }

        text.text = value;
        text.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }
}
