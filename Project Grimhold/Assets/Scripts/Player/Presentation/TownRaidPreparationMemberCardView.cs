using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only authored card with an explicit remote-member selection intention.</summary>
[DisallowMultipleComponent]
public sealed class TownRaidPreparationMemberCardView : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _stateText;
    [SerializeField] private GameObject _hostIndicator;
    [SerializeField] private Button _selectionButton;
    [SerializeField] private Image _mainHandIcon;
    [SerializeField] private Image _offHandIcon;
    [SerializeField] private Image _armorIcon;
    [SerializeField] private EquipmentSlotIconPresentation _mainHandCategory = new();
    [SerializeField] private EquipmentSlotIconPresentation _offHandCategory = new();
    [SerializeField] private EquipmentSlotIconPresentation _armorCategory = new();
    [SerializeField] private Image _baseHead;
    [SerializeField] private Image _baseBody;
    [SerializeField] private Image _baseLeftHand;
    [SerializeField] private Image _baseRightHand;
    [SerializeField] private Image _baseLegs;
    [SerializeField] private Image _helmet;
    [SerializeField] private Image _armor;
    [SerializeField] private Image _leftGlove;
    [SerializeField] private Image _rightGlove;
    [SerializeField] private Image _boots;

    private bool _valid;
    private bool _canSelect;
    private ProfileId _profileId;
    public event Action<ProfileId> Selected;

    private void Awake()
    {
        _valid = _nameText != null && _stateText != null && _hostIndicator != null &&
            _selectionButton != null && _mainHandIcon != null && _offHandIcon != null && _armorIcon != null &&
            _baseHead != null && _baseBody != null && _baseLeftHand != null && _baseRightHand != null &&
            _baseLegs != null && _helmet != null && _armor != null && _leftGlove != null &&
            _rightGlove != null && _boots != null;
        if (!_valid)
        {
            Debug.LogError("TownRaidPreparationMemberCardView requires all serialized text, selection, summary icon and body layer references.", this);
            if (_selectionButton != null) _selectionButton.interactable = false;
            return;
        }
        _selectionButton.onClick.AddListener(SelectMember);
    }

    private void OnDestroy()
    {
        if (_selectionButton != null) _selectionButton.onClick.RemoveListener(SelectMember);
    }

    public void Present(in TownRaidPreparationPresentation.Member member)
    {
        if (!_valid) return;
        _profileId = member.ProfileId;
        _canSelect = !member.IsLocal;
        _selectionButton.interactable = _canSelect;
        _nameText.text = member.DisplayName;
        _stateText.text = TownRaidPreparationMemberCardRules.StateLabel(member.IsReady);
        _hostIndicator.SetActive(member.IsHost);
        PresentSummary(_mainHandIcon, _mainHandCategory, member, EquipmentSlot.WeaponSetAMainHand);
        PresentSummary(_offHandIcon, _offHandCategory, member, EquipmentSlot.WeaponSetAOffHand);
        PresentSummary(_armorIcon, _armorCategory, member, EquipmentSlot.Armor);
        PresentOverlay(_helmet, _baseHead, member, EquipmentSlot.Helmet);
        PresentOverlay(_armor, _baseBody, member, EquipmentSlot.Armor);
        PresentOverlay(_leftGlove, _baseLeftHand, member, EquipmentSlot.Gloves);
        PresentOverlay(_rightGlove, _baseRightHand, member, EquipmentSlot.Gloves);
        PresentOverlay(_boots, _baseLegs, member, EquipmentSlot.Boots);
    }

    private void SelectMember()
    {
        if (_canSelect) Selected?.Invoke(_profileId);
    }

    private static void PresentSummary(Image image, EquipmentSlotIconPresentation category, in TownRaidPreparationPresentation.Member member, EquipmentSlot slot)
    {
        var definition = TownRaidPreparationMemberCardRules.ResolveSummarySlot(member, slot, out bool reserved);
        if (category?.TryPresent(image, definition != null ? definition.Icon : null,
            definition == null && !reserved, reserved ? 0.35f : 1f) == true) return;
        image.sprite = definition != null ? definition.Icon : null;
        image.enabled = image.sprite != null;
        image.color = reserved ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
    }

    private static void PresentOverlay(Image overlay, Image baseLayer,
        in TownRaidPreparationPresentation.Member member, EquipmentSlot slot)
    {
        member.TryGetEquipment(slot, out LootDefinition definition);
        overlay.enabled = TownRaidPreparationMemberCardRules.ResolveOverlay(baseLayer.sprite, definition, out Sprite sprite, out Color color);
        overlay.sprite = sprite;
        overlay.color = color;
    }
}
