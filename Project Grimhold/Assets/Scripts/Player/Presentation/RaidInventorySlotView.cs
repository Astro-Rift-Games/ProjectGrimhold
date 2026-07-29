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

    public event Action<LootId, LootTransferQuantityMode> SelectionRequested;
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
        if (_button != null)
        {
            _button.interactable = interactable && _isOccupied;
        }

        if (_background != null)
        {
            _background.color = selected && _isOccupied ? _selectedColor : _normalColor;
        }
    }

    private void OnClicked()
    {
        if (_isOccupied && _button != null && _button.interactable && _lootId.IsValid)
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

        SelectionRequested?.Invoke(_lootId, LootTransferQuantityMode.FullStack);
    }
}
