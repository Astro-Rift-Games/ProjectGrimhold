using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presents already-replicated member equipment without gameplay actions.</summary>
[DisallowMultipleComponent]
public sealed class TownRaidPreparationInspectionView : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TownRaidPreparationInspectionSlotView[] _slots;
    [SerializeField] private EquipmentTooltipView _tooltip;

    private bool _configured;
    private TownRaidPreparationInspectionSlotView _hoveredSlot;
    public bool IsOpen { get; private set; }
    public ProfileId InspectedProfileId { get; private set; }

    private void Awake()
    {
        _configured = _root != null && _titleText != null && _closeButton != null &&
            _tooltip != null && _slots != null && _slots.Length == EquipmentSlotRules.SlotCount;
        if (_slots != null)
        {
            for (int index = 0; index < _slots.Length; index++)
            {
                var slot = _slots[index];
                if (slot == null) { _configured = false; continue; }
                if (!EquipmentSlotRules.IsEquipmentSlot(slot.Slot) || !(slot.transform is RectTransform))
                    _configured = false;
                for (int previous = 0; previous < index; previous++)
                    if (_slots[previous] != null && _slots[previous].Slot == slot.Slot) _configured = false;
                slot.HoverStarted += OnHoverStarted;
                slot.HoverEnded += OnHoverEnded;
            }
        }
        if (!_configured)
            Debug.LogError("TownRaidPreparationInspectionView requires root, title, close button, tooltip and eight unique equipment slot references with RectTransforms.", this);
        if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        Hide();
    }

    public void Present(in TownRaidPreparationPresentation.Member member)
    {
        if (!_configured) return;
        HideTooltip();
        InspectedProfileId = member.ProfileId;
        _titleText.text = $"Equipamiento de {member.DisplayName}";
        for (int index = 0; index < _slots.Length; index++)
        {
            var slot = _slots[index];
            member.TryGetEquipment(slot.Slot, out LootDefinition definition);
            bool reserved = false;
            if (slot.Slot == EquipmentSlot.WeaponSetAOffHand || slot.Slot == EquipmentSlot.WeaponSetBOffHand)
            {
                EquipmentSlot mainSlot = slot.Slot == EquipmentSlot.WeaponSetAOffHand
                    ? EquipmentSlot.WeaponSetAMainHand : EquipmentSlot.WeaponSetBMainHand;
                member.TryGetEquipment(mainSlot, out LootDefinition main);
                definition = TownRaidPreparationMemberCardRules.ResolveOffHand(main, definition, out reserved);
            }
            slot.Present(definition, reserved);
        }
        IsOpen = true;
        _root.SetActive(true);
    }

    public void Hide()
    {
        IsOpen = false;
        InspectedProfileId = default;
        HideTooltip();
        if (_root != null) _root.SetActive(false);
    }

    private void OnDisable() => Hide();

    private void OnHoverStarted(TownRaidPreparationInspectionSlotView slot)
    {
        if (!IsOpen || slot.Definition == null || _tooltip == null) return;
        _hoveredSlot = slot;
        _tooltip.Show(EquipmentTooltipPresentationBuilder.Build(slot.Definition), slot.transform as RectTransform);
    }

    private void OnHoverEnded(TownRaidPreparationInspectionSlotView slot)
    {
        if (_hoveredSlot == slot) HideTooltip();
    }

    private void HideTooltip()
    {
        _hoveredSlot = null;
        if (_tooltip != null) _tooltip.Hide();
    }

    private void OnDestroy()
    {
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
        if (_slots == null) return;
        for (int index = 0; index < _slots.Length; index++)
        {
            if (_slots[index] == null) continue;
            _slots[index].HoverStarted -= OnHoverStarted;
            _slots[index].HoverEnded -= OnHoverEnded;
        }
    }
}
