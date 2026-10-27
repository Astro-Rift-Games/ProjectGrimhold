using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Read-only equipment icon and hover source for member inspection.</summary>
[DisallowMultipleComponent]
public sealed class TownRaidPreparationInspectionSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private EquipmentSlot _slot;
    [SerializeField] private Image _icon;
    [SerializeField] private EquipmentSlotIconPresentation _emptyCategory = new();

    public EquipmentSlot Slot => _slot;
    public LootDefinition Definition { get; private set; }
    public event Action<TownRaidPreparationInspectionSlotView> HoverStarted;
    public event Action<TownRaidPreparationInspectionSlotView> HoverEnded;

    public void Present(LootDefinition definition, bool reserved)
    {
        Definition = definition;
        if (_icon == null) return;
        if (_emptyCategory?.TryPresent(_icon, definition != null ? definition.Icon : null,
            definition == null && !reserved, reserved ? 0.35f : 1f) == true) return;
        _icon.sprite = definition != null ? definition.Icon : null;
        _icon.enabled = _icon.sprite != null;
        Color color = _icon.color;
        color.a = reserved ? 0.35f : 1f;
        _icon.color = color;
    }

    public void Clear() => Present(null, false);

    public void OnPointerEnter(PointerEventData eventData) => HoverStarted?.Invoke(this);
    public void OnPointerExit(PointerEventData eventData) => HoverEnded?.Invoke(this);
}
