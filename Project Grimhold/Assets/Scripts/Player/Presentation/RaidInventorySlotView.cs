using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one reusable occupied or empty raid-inventory slot.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidInventorySlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField]
    private Image _icon;

    [SerializeField]
    private TMP_Text _nameText;

    [SerializeField]
    private TMP_Text _amountText;

    [SerializeField]
    private Button _button;

    [SerializeField]
    private Image _background;

    [SerializeField]
    private Color _normalColor = new(0.12f, 0.12f, 0.14f, 0.95f);

    [SerializeField]
    private Color _selectedColor = new(0.28f, 0.22f, 0.08f, 1f);

    private LootId _lootId;
    private bool _isOccupied;
    private RaidLootSlotInteractionMode _interactionMode;
    private EquipmentTooltipPresentation _tooltip;
    private RaidInventorySlotData _slotData;
    private DragSlotLocation _dragLocation;
    private EquipmentSlot _assignedEquipmentSlot;
    private bool _isDragging;

    public event Action<LootId, LootTransferQuantityMode> SelectionRequested;
    public event Action<LootId, RectTransform> ContextRequested;
    public event Action<EquipmentTooltipPresentation, RectTransform> TooltipRequested;
    public event Action<RectTransform> TooltipDismissRequested;
    
    public event Action<DragPayload> DragStarted;
    public event Action DragUpdated;
    public event Action<bool> DragEnded;
    public event Action<DragSlotLocation, EquipmentSlot> DropReceived;

    public LootId LootId => _lootId;
    public bool IsOccupied => _isOccupied;

    private void Awake()
    {
        // Left click transfer disabled in favor of Drag and Drop
    }

    private void OnDestroy()
    {
        // Left click transfer disabled
    }

    public void Present(in RaidInventorySlotData data)
    {
        if (!data.IsOccupied)
        {
            Clear();
            return;
        }

        if (_isOccupied && _lootId != data.LootId)
        {
            TooltipDismissRequested?.Invoke(transform as RectTransform);
        }

        _lootId = data.LootId;
        _isOccupied = true;
        _tooltip = data.Tooltip;
        _slotData = data;

        if (_icon != null)
        {
            _icon.sprite = data.Icon;
            _icon.enabled = data.Icon != null;
        }

        if (_nameText != null)
        {
            _nameText.text = data.DisplayName;
        }

        if (_amountText != null)
        {
            _amountText.text = $"× {data.Amount}";
        }
    }

    public void PresentWeaponSetSlot(
        WeaponSetSlot slot,
        in RaidInventorySlotData data,
        bool isActive,
        bool canUnequip) =>
        PresentEquipmentSlot(EquipmentSlotRules.GetMainHandSlot(slot), in data, isActive, canUnequip);

    public void PresentEquipmentSlot(
        EquipmentSlot slot,
        in RaidInventorySlotData data,
        bool isActive,
        bool canUnequip,
        bool isBlocked = false)
    {
        string slotLabel = ResolveSlotLabel(slot);
        if (isBlocked)
        {
            Clear();
            if (_nameText != null) _nameText.text = $"{slotLabel}\nBloqueado (arma 2M)";
            return;
        }
        if (!data.IsOccupied)
        {
            Clear();
            if (_nameText != null) _nameText.text = $"{slotLabel}\nVacío";
            if (_background != null) _background.color = _normalColor;
            return;
        }

        Present(in data);
        if (_nameText != null) _nameText.text = $"{slotLabel}\n{data.DisplayName}";
        if (_amountText != null) _amountText.text = isActive ? "Activa · Desequipar" : "Desequipar";
        SetInteraction(
            canUnequip ? RaidLootSlotInteractionMode.TransferWithContextMenu : RaidLootSlotInteractionMode.ReadOnly,
            isActive);
    }

    private static string ResolveSlotLabel(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.WeaponSetAMainHand => "Set A / Main Hand",
        EquipmentSlot.WeaponSetBMainHand => "Set B / Main Hand",
        EquipmentSlot.WeaponSetAOffHand => "Set A / Off Hand",
        EquipmentSlot.WeaponSetBOffHand => "Set B / Off Hand",
        EquipmentSlot.Helmet => "Casco",
        EquipmentSlot.Armor => "Armadura",
        EquipmentSlot.Gloves => "Guantes",
        EquipmentSlot.Boots => "Botas",
        _ => "Equipment"
    };

    public void Clear()
    {
        if (_isOccupied)
        {
            TooltipDismissRequested?.Invoke(transform as RectTransform);
        }
        _lootId = default;
        _isOccupied = false;
        _tooltip = default;
        _slotData = default;
        if (_icon != null)
        {
            _icon.sprite = null;
            _icon.enabled = false;
        }

        if (_nameText != null)
        {
            _nameText.text = string.Empty;
        }

        if (_amountText != null)
        {
            _amountText.text = string.Empty;
        }
        SetInteraction(false, false);
    }

    public void SetInteraction(bool interactable, bool selected)
    {
        SetInteraction(
            interactable
                ? RaidLootSlotInteractionMode.Transfer
                : RaidLootSlotInteractionMode.ReadOnly,
            selected);
    }

    public void SetInteraction(RaidLootSlotInteractionMode mode, bool selected)
    {
        _interactionMode = _isOccupied ? mode : RaidLootSlotInteractionMode.ReadOnly;
        if (_button != null)
        {
            _button.interactable = _interactionMode != RaidLootSlotInteractionMode.ReadOnly;
        }

        if (_background != null)
        {
            _background.color = selected && _isOccupied ? _selectedColor : _normalColor;
        }
    }

    /// <summary>
    /// Converts a right click on an interactive occupied slot into a full-stack intention.
    /// Left clicks continue through <see cref="Button.onClick"/> so keyboard submit behavior is preserved.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Right ||
            !_isOccupied || _button == null || !_button.interactable || !_lootId.IsValid)
        {
            return;
        }

        if (_interactionMode == RaidLootSlotInteractionMode.ContextMenu ||
            _interactionMode == RaidLootSlotInteractionMode.TransferWithContextMenu)
        {
            TooltipDismissRequested?.Invoke(transform as RectTransform);
            ContextRequested?.Invoke(_lootId, transform as RectTransform);
            return;
        }

        if (_interactionMode == RaidLootSlotInteractionMode.Transfer)
        {
            SelectionRequested?.Invoke(_lootId, LootTransferQuantityMode.FullStack);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isOccupied && _tooltip.CanShow)
        {
            TooltipRequested?.Invoke(_tooltip, transform as RectTransform);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipDismissRequested?.Invoke(transform as RectTransform);
    }

    public void SetDragLocation(DragSlotLocation location, EquipmentSlot equipmentSlot = EquipmentSlot.None)
    {
        _dragLocation = location;
        _assignedEquipmentSlot = equipmentSlot;
    }

    public void SetDropHighlight(DropHighlightState state)
    {
        if (_background == null) return;
        
        switch (state)
        {
            case DropHighlightState.None:
                _background.color = _isOccupied && _button != null && !_button.interactable ? _normalColor : _normalColor; // Simplified for now, relies on SetInteraction to restore proper color later if needed
                // Better approach: just trigger a re-eval of interaction state
                if (_isOccupied)
                {
                    _background.color = _normalColor;
                }
                break;
            case DropHighlightState.Valid:
                _background.color = new Color(0.2f, 0.6f, 0.2f, 0.8f);
                break;
            case DropHighlightState.Invalid:
                _background.color = new Color(0.6f, 0.2f, 0.2f, 0.8f);
                break;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"OnBeginDrag on slot {_lootId.Value} occupied={_isOccupied} buttonInt={_button?.interactable} dragLoc={_dragLocation}");
        if (eventData.button != PointerEventData.InputButton.Left || !_isOccupied || _button == null || !_button.interactable || !_lootId.IsValid || _dragLocation == DragSlotLocation.None)
        {
            return;
        }

        _isDragging = true;
        TooltipDismissRequested?.Invoke(transform as RectTransform);
        
        var payload = DragPayload.Create(_slotData, _dragLocation, _assignedEquipmentSlot);
        DragStarted?.Invoke(payload);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_isDragging)
        {
            DragUpdated?.Invoke();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        _isDragging = false;
        
        // The drop target will handle the drop if valid. We just notify end.
        DragEnded?.Invoke(eventData.pointerCurrentRaycast.gameObject != null);
    }

    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log($"OnDrop on slot {_lootId.Value} dragLoc={_dragLocation}");
        if (_dragLocation != DragSlotLocation.None)
        {
            DropReceived?.Invoke(_dragLocation, _assignedEquipmentSlot);
        }
    }
}
