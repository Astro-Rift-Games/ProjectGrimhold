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

    private bool _isReserved;
    private bool _isInactiveSet;
    private const float ReservedIconAlpha = 0.35f;
    private const float InactiveSetAlpha = 0.45f;

    public bool IsEquipmentDropReserved => _isReserved;

    private LootId _lootId;
    private bool _isOccupied;
    private RaidLootSlotInteractionMode _interactionMode;
    private EquipmentTooltipPresentation _tooltip;
    private RaidInventorySlotData _slotData;
    private DragSlotLocation _dragLocation;
    private EquipmentSlot _assignedEquipmentSlot;
    private bool _isDragging;
    private bool _isSelected;
    private DropHighlightState _dropHighlight;
    private float _transferPulseRemaining;
    private const float TransferPulseDuration = 0.2f;

    public bool IsTransferFeedbackActive => _transferPulseRemaining > 0f;

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
            ClearTransferFeedback();
            TooltipDismissRequested?.Invoke(transform as RectTransform);
        }

        _lootId = data.LootId;
        _isOccupied = true;
        _tooltip = data.Tooltip;
        _slotData = data;

        if (_icon != null)
        {
            _icon.color = new Color(_icon.color.r, _icon.color.g, _icon.color.b, 1f);
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
        bool isBlocked = false,
        bool isInactiveSet = false)
    {
        string slotLabel = ResolveSlotLabel(slot);
        if (isBlocked)
        {
            Clear();
            _isReserved = true;
            if (_icon != null)
            {
                _icon.sprite = data.Icon;
                _icon.enabled = data.Icon != null;
                _icon.color = new Color(_icon.color.r, _icon.color.g, _icon.color.b, ReservedIconAlpha);
            }
            if (_nameText != null) _nameText.text = $"{slotLabel}\nReservado por {data.DisplayName}";
            SetWeaponSetVisual(isActive, isInactiveSet);
            return;
        }
        if (!data.IsOccupied)
        {
            Clear();
            if (_nameText != null) _nameText.text = $"{slotLabel}\nVacío";
            SetWeaponSetVisual(isActive, isInactiveSet);
            return;
        }

        _isReserved = false;
        Present(in data);
        if (_nameText != null) _nameText.text = $"{slotLabel}\n{data.DisplayName}";
        if (_amountText != null) _amountText.text = isActive ? "Activa · Desequipar" : "Desequipar";
        SetInteraction(
            canUnequip ? RaidLootSlotInteractionMode.TransferWithContextMenu : RaidLootSlotInteractionMode.ReadOnly,
            isActive);
        SetWeaponSetVisual(isActive, isInactiveSet);
    }

    private void SetWeaponSetVisual(bool active, bool inactive)
    {
        _isSelected = active;
        _isInactiveSet = inactive;
        RefreshBackground();
    }

    /// <summary>
    /// Presents one Quick Slot binding. A bound definition with no available units stays visible
    /// with a zero amount so the player sees it is currently unavailable.
    /// </summary>
    public void PresentQuickSlot(
        int slotIndex,
        bool assigned,
        LootId lootId,
        Sprite icon,
        string displayName,
        int amount,
        bool canClear)
    {
        Clear();
        string slotLabel = $"Quick Slot {slotIndex + 1}";
        if (!assigned)
        {
            if (_nameText != null) _nameText.text = $"{slotLabel}\nVacío";
            return;
        }

        _lootId = lootId;
        _isOccupied = true;
        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
        }

        if (_nameText != null) _nameText.text = $"{slotLabel}\n{displayName}";
        if (_amountText != null) _amountText.text = $"× {amount}";
        SetInteraction(canClear ? RaidLootSlotInteractionMode.Transfer : RaidLootSlotInteractionMode.ReadOnly, false);
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
        _isReserved = false;
        _isInactiveSet = false;
        _transferPulseRemaining = 0f;
        _dropHighlight = DropHighlightState.None;
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
            _icon.color = new Color(_icon.color.r, _icon.color.g, _icon.color.b, 1f);
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
        _isSelected = selected;
        _interactionMode = _isOccupied ? mode : RaidLootSlotInteractionMode.ReadOnly;
        if (_button != null)
        {
            _button.interactable = _interactionMode != RaidLootSlotInteractionMode.ReadOnly;
        }

        RefreshBackground();
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
        _dropHighlight = _isReserved && state == DropHighlightState.Valid ? DropHighlightState.Invalid : state;
        RefreshBackground();
    }

    public void ShowTransferSuccess()
    {
        if (!_isOccupied) return;
        _transferPulseRemaining = TransferPulseDuration;
        RefreshBackground();
    }

    public void ClearTransferFeedback()
    {
        _transferPulseRemaining = 0f;
        RefreshBackground();
    }

    private void OnDisable() => ClearTransferFeedback();

    private void Update()
    {
        if (_transferPulseRemaining <= 0f) return;
        _transferPulseRemaining = Mathf.Max(0f, _transferPulseRemaining - Time.unscaledDeltaTime);
        RefreshBackground();
    }

    private void RefreshBackground()
    {
        if (_background == null) return;
        Color restingColor = _dropHighlight switch
        {
            DropHighlightState.Valid => new Color(0.2f, 0.6f, 0.2f, 0.8f),
            DropHighlightState.Invalid => new Color(0.6f, 0.2f, 0.2f, 0.8f),
            _ => _isSelected ? _selectedColor : _normalColor
        };
        if (_isInactiveSet && _dropHighlight == DropHighlightState.None)
            restingColor.a *= InactiveSetAlpha;
        _background.color = Color.Lerp(restingColor, new Color(0.25f, 0.8f, 0.4f, 1f),
            _transferPulseRemaining / TransferPulseDuration);
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
        if (!_isReserved && _dragLocation != DragSlotLocation.None)
        {
            DropReceived?.Invoke(_dragLocation, _assignedEquipmentSlot);
        }
    }
}
