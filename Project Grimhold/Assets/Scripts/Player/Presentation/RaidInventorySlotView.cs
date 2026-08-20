using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders one reusable occupied or empty raid-inventory slot.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidInventorySlotView : MonoBehaviour, IPointerClickHandler
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

    public event Action<LootId, LootTransferQuantityMode> SelectionRequested;
    public event Action<LootId, Vector2> ContextRequested;
    public LootId LootId => _lootId;
    public bool IsOccupied => _isOccupied;

    private void Awake()
    {
        if (_button != null)
        {
            _button.onClick.AddListener(OnClicked);
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnClicked);
        }
    }

    public void Present(in RaidInventorySlotData data)
    {
        if (!data.IsOccupied)
        {
            Clear();
            return;
        }

        _lootId = data.LootId;
        _isOccupied = true;

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

    public void PresentWeaponSlot(
        WeaponSlot slot,
        in RaidInventorySlotData data,
        bool isActive,
        bool canUnequip) =>
        PresentEquipmentSlot(EquipmentSlotRules.FromWeaponSlot(slot), in data, isActive, canUnequip);

    public void PresentEquipmentSlot(
        EquipmentSlot slot,
        in RaidInventorySlotData data,
        bool isActive,
        bool canUnequip)
    {
        string slotLabel = ResolveSlotLabel(slot);
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
            canUnequip ? RaidLootSlotInteractionMode.Transfer : RaidLootSlotInteractionMode.ReadOnly,
            isActive);
    }

    private static string ResolveSlotLabel(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.WeaponSlot1 => "Weapon Slot 1",
        EquipmentSlot.WeaponSlot2 => "Weapon Slot 2",
        EquipmentSlot.Helmet => "Casco",
        EquipmentSlot.Armor => "Armadura",
        EquipmentSlot.Gloves => "Guantes",
        EquipmentSlot.Boots => "Botas",
        _ => "Equipment"
    };

    public void Clear()
    {
        _lootId = default;
        _isOccupied = false;
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

    private void OnClicked()
    {
        if ((_interactionMode == RaidLootSlotInteractionMode.Transfer ||
             _interactionMode == RaidLootSlotInteractionMode.TransferWithContextMenu) && _isOccupied &&
            _button != null && _button.interactable && _lootId.IsValid)
        {
            SelectionRequested?.Invoke(_lootId, LootTransferQuantityMode.SingleUnit);
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
            ContextRequested?.Invoke(_lootId, eventData.position);
            return;
        }

        if (_interactionMode == RaidLootSlotInteractionMode.Transfer)
        {
            SelectionRequested?.Invoke(_lootId, LootTransferQuantityMode.FullStack);
        }
    }
}
