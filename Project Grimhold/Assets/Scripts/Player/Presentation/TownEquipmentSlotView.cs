using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only cell of the Town character sheet showing what is prepared in one equipment slot.</summary>
[DisallowMultipleComponent]
public sealed class TownEquipmentSlotView : MonoBehaviour
{
    [SerializeField] private EquipmentSlot _slot;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;

    private bool _hasItem;
    private string _itemName = string.Empty;

    public EquipmentSlot Slot => _slot;
    public bool HasItem => _hasItem;
    public string ItemName => _itemName;

    public void Present(in TownEquipmentSlotEntry entry)
    {
        _hasItem = !entry.IsEmpty;
        _itemName = entry.DisplayName ?? string.Empty;
        Apply(entry.Icon);
    }

    public void PresentEmpty()
    {
        _hasItem = false;
        _itemName = string.Empty;
        Apply(null);
    }

    private void Apply(Sprite icon)
    {
        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
        }

        if (_nameText != null)
        {
            _nameText.text = _itemName;
        }
    }
}
