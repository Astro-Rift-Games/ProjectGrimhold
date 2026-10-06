using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One universal ability slot panel of the "Active Loadout".</summary>
[DisallowMultipleComponent]
public sealed class TownAbilitySlotView : MonoBehaviour
{
    [SerializeField] private UniversalAbilitySlot _slot = UniversalAbilitySlot.Slot1;
    [SerializeField] private TMP_Text _headerText;
    [SerializeField] private TMP_Text _keyText;
    [SerializeField] private Image _icon;
    [SerializeField] private GameObject _iconPlaceholder;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _detailText;
    [SerializeField] private Button _clearButton;

    public UniversalAbilitySlot Slot => _slot;
    public TMP_Text NameText => _nameText;
    public TMP_Text DetailText => _detailText;
    public Button ClearButton => _clearButton;

    public event Action<UniversalAbilitySlot> ClearRequested;

    private void Awake()
    {
        if (_clearButton != null)
        {
            _clearButton.onClick.AddListener(OnClearClicked);
        }
    }

    private void OnDestroy()
    {
        if (_clearButton != null)
        {
            _clearButton.onClick.RemoveListener(OnClearClicked);
        }
    }

    /// <summary>Shows the prepared entry, or the empty state when <paramref name="entry"/> is null.</summary>
    public void Present(TownAbilityEntry? entry, bool canClear)
    {
        int number = _slot == UniversalAbilitySlot.Slot2 ? 2 : 1;
        _headerText.text = $"Slot {number}";
        _keyText.text = TownAbilitySlotKeyLabels.For(_slot);
        if (entry.HasValue)
        {
            TownAbilityEntry value = entry.Value;
            _nameText.text = value.DisplayName;
            _detailText.text = TownAbilityText.SlotDetail(value);
            TownAbilityIconUtility.Apply(_icon, _iconPlaceholder, value.Icon);
        }
        else
        {
            _nameText.text = string.Empty;
            _detailText.text = "Empty";
            TownAbilityIconUtility.Apply(_icon, _iconPlaceholder, null);
        }

        _clearButton.interactable = entry.HasValue && canClear;
        TMP_Text clearLabel = _clearButton.GetComponentInChildren<TMP_Text>(true);
        if (clearLabel != null)
        {
            clearLabel.alpha = _clearButton.interactable ? 1f : 0.5f;
        }
    }

    private void OnClearClicked() => ClearRequested?.Invoke(_slot);
}
