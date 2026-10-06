using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Passive content of the Town menu Abilities tab: unlocked ability list with a resource filter, the
/// selected ability's details, the two universal slots, the requirement check and the preparation rules.
/// It never reads the profile; the presenter pushes everything through <see cref="Present"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownAbilitiesView : MonoBehaviour
{
    private const string EmptyRepertoireText = "No abilities unlocked yet.";
    private const string EmptyFilterText = "No abilities in this category.";
    private const string NoRequirementsText = "No attribute requirements.";
    private const string SelectAbilityText = "Select an ability";
    private const float DisabledLabelAlpha = 0.5f;

    [SerializeField] private GameObject _contentRoot;
    [SerializeField] private GameObject _unavailableNote;

    [Header("Ability list")]
    [SerializeField] private Button[] _filterButtons;
    [SerializeField] private TMP_Text _listNote;
    [SerializeField] private RectTransform _cardsRoot;
    [SerializeField] private TownAbilityCardView _cardTemplate;

    [Header("Details")]
    [SerializeField] private GameObject _detailsRoot;
    [SerializeField] private TMP_Text _detailsNote;
    [SerializeField] private Image _detailIcon;
    [SerializeField] private GameObject _detailIconPlaceholder;
    [SerializeField] private TMP_Text _detailNameText;
    [SerializeField] private TMP_Text _detailSubtitleText;
    [SerializeField] private TMP_Text _detailDescriptionText;
    [SerializeField] private TMP_Text _requirementValueText;
    [SerializeField] private TMP_Text _resourceValueText;
    [SerializeField] private TMP_Text _cooldownValueText;
    [SerializeField] private Button[] _equipButtons;
    [SerializeField] private TMP_Text[] _equipLabels;

    [Header("Loadout and rules")]
    [SerializeField] private TownAbilitySlotView[] _slotViews;
    [SerializeField] private RectTransform _requirementRowsRoot;
    [SerializeField] private TownAbilityRequirementRowView _requirementRowTemplate;
    [SerializeField] private TMP_Text _requirementsNote;
    [SerializeField] private GameObject _lockedNote;
    [SerializeField] private Image[] _previewIcons;
    [SerializeField] private TMP_Text[] _previewKeyTexts;

    [Header("Filter highlight")]
    [SerializeField] private Color _selectedFilterColor = new(0.85f, 0.65f, 0.25f, 1f);
    [SerializeField] private Color _normalFilterColor = new(0.25f, 0.2f, 0.15f, 1f);

    private readonly List<TownAbilityCardView> _cards = new();
    private readonly List<TownAbilityRequirementRowView> _requirementRows = new();
    private readonly List<Action> _unsubscribe = new();
    private AbilityId _shownAbility;

    public IReadOnlyList<TownAbilityCardView> Cards => _cards;
    public IReadOnlyList<TownAbilityRequirementRowView> RequirementRows => _requirementRows;
    public GameObject ContentRoot => _contentRoot;
    public GameObject UnavailableNote => _unavailableNote;
    public TMP_Text ListNote => _listNote;
    public GameObject DetailsRoot => _detailsRoot;
    public TMP_Text DetailsNote => _detailsNote;
    public TMP_Text DetailNameText => _detailNameText;
    public GameObject LockedNote => _lockedNote;

    public event Action<TownAbilitiesFilter> FilterRequested;
    public event Action<AbilityId> AbilitySelected;
    public event Action<AbilityId, UniversalAbilitySlot> EquipRequested;
    public event Action<UniversalAbilitySlot> ClearRequested;

    public Button FilterButton(TownAbilitiesFilter filter) => _filterButtons[(int)filter];

    public Button EquipButton(UniversalAbilitySlot slot) => _equipButtons[SlotIndex(slot)];

    public string EquipButtonText(UniversalAbilitySlot slot) => _equipLabels[SlotIndex(slot)].text;

    public TownAbilitySlotView SlotView(UniversalAbilitySlot slot) => _slotViews[SlotIndex(slot)];

    private void Awake()
    {
        for (int index = 0; index < _filterButtons.Length; index++)
        {
            var filter = (TownAbilitiesFilter)index;
            Subscribe(_filterButtons[index], () => FilterRequested?.Invoke(filter));
        }

        for (int index = 0; index < _equipButtons.Length; index++)
        {
            UniversalAbilitySlot slot = SlotAt(index);
            Subscribe(_equipButtons[index], () => OnEquipClicked(slot));
        }

        foreach (TownAbilitySlotView slotView in _slotViews)
        {
            slotView.ClearRequested += OnSlotClearRequested;
            TownAbilitySlotView captured = slotView;
            _unsubscribe.Add(() => captured.ClearRequested -= OnSlotClearRequested);
        }
    }

    private void OnDestroy()
    {
        foreach (Action unsubscribe in _unsubscribe)
        {
            unsubscribe();
        }

        _unsubscribe.Clear();
    }

    public void Open() => gameObject.SetActive(true);

    public void Close() => gameObject.SetActive(false);

    /// <summary>Replaces the whole tab with an "Abilities unavailable" note.</summary>
    public void PresentUnavailable()
    {
        _contentRoot.SetActive(false);
        _unavailableNote.SetActive(true);
    }

    public void Present(
        in TownAbilitiesPresentation presentation,
        TownAbilitiesFilter filter,
        AbilityId selectedId,
        bool canMutate,
        Func<AbilityId, UniversalAbilitySlot, bool> canEquip)
    {
        _contentRoot.SetActive(true);
        _unavailableNote.SetActive(false);
        _shownAbility = selectedId;

        PresentFilters(filter);
        IReadOnlyList<TownAbilityEntry> visible = presentation.Filtered(filter);
        PresentList(presentation.Entries.Count == 0, visible, selectedId);

        TownAbilityEntry? selected = Find(presentation.Entries, selectedId);
        PresentDetails(selected, presentation, canMutate, canEquip);
        PresentRequirements(selected);
        PresentSlots(presentation, canMutate);
        _lockedNote.SetActive(!canMutate);
    }

    private void PresentFilters(TownAbilitiesFilter filter)
    {
        for (int index = 0; index < _filterButtons.Length; index++)
        {
            Graphic graphic = _filterButtons[index].targetGraphic;
            if (graphic != null)
            {
                graphic.color = index == (int)filter ? _selectedFilterColor : _normalFilterColor;
            }
        }
    }

    private void PresentList(bool repertoireEmpty, IReadOnlyList<TownAbilityEntry> visible, AbilityId selectedId)
    {
        Discard(_cards);
        for (int index = 0; index < visible.Count; index++)
        {
            TownAbilityCardView card = Instantiate(_cardTemplate, _cardsRoot, false);
            card.gameObject.SetActive(true);
            card.Present(visible[index], visible[index].Id == selectedId);
            card.Clicked += OnCardClicked;
            _cards.Add(card);
        }

        _listNote.gameObject.SetActive(visible.Count == 0);
        _listNote.text = repertoireEmpty ? EmptyRepertoireText : EmptyFilterText;
    }

    private void PresentDetails(
        TownAbilityEntry? selected,
        in TownAbilitiesPresentation presentation,
        bool canMutate,
        Func<AbilityId, UniversalAbilitySlot, bool> canEquip)
    {
        _detailsRoot.SetActive(selected.HasValue);
        _detailsNote.gameObject.SetActive(!selected.HasValue);
        _detailsNote.text = SelectAbilityText;
        if (!selected.HasValue)
        {
            return;
        }

        TownAbilityEntry entry = selected.Value;
        TownAbilityIconUtility.Apply(_detailIcon, _detailIconPlaceholder, entry.Icon);
        _detailNameText.text = entry.DisplayName;
        _detailSubtitleText.text = "Unlocked Active Ability";
        _detailDescriptionText.text = entry.Description;
        _requirementValueText.text = TownAbilityText.Requirement(entry);
        _resourceValueText.text = TownAbilityText.ResourceCost(entry);
        _cooldownValueText.text = TownAbilityText.Cooldown(entry.CooldownSeconds);

        for (int index = 0; index < _equipButtons.Length; index++)
        {
            UniversalAbilitySlot slot = SlotAt(index);
            bool inThisSlot = entry.HasEquippedSlot && entry.EquippedSlot == slot;
            _equipLabels[index].text = TownAbilityText.EquipLabel(slot, inThisSlot);
            bool interactable = canMutate && !inThisSlot && canEquip != null && canEquip(entry.Id, slot);
            _equipButtons[index].interactable = interactable;
            _equipLabels[index].alpha = interactable ? 1f : DisabledLabelAlpha;
        }
    }

    private void PresentRequirements(TownAbilityEntry? selected)
    {
        Discard(_requirementRows);
        if (!selected.HasValue || selected.Value.Requirements.Count == 0)
        {
            _requirementsNote.gameObject.SetActive(true);
            _requirementsNote.text = selected.HasValue ? NoRequirementsText : SelectAbilityText;
            return;
        }

        _requirementsNote.gameObject.SetActive(false);
        TownAbilityEntry entry = selected.Value;
        for (int index = 0; index < entry.Requirements.Count; index++)
        {
            TownAbilityRequirementRowView row = Instantiate(_requirementRowTemplate, _requirementRowsRoot, false);
            row.gameObject.SetActive(true);
            row.Present(entry.Requirements[index], entry.DisplayName);
            _requirementRows.Add(row);
        }
    }

    private void PresentSlots(in TownAbilitiesPresentation presentation, bool canMutate)
    {
        for (int index = 0; index < _slotViews.Length; index++)
        {
            UniversalAbilitySlot slot = SlotAt(index);
            TownAbilityEntry? entry = presentation.GetSlot(slot);
            _slotViews[index].Present(entry, canMutate);
            TownAbilityIconUtility.Apply(_previewIcons[index], null, entry?.Icon);
            _previewKeyTexts[index].text = TownAbilitySlotKeyLabels.For(slot);
        }
    }

    private static TownAbilityEntry? Find(IReadOnlyList<TownAbilityEntry> entries, AbilityId id)
    {
        if (!id.IsValid)
        {
            return null;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index].Id == id)
            {
                return entries[index];
            }
        }

        return null;
    }

    private void OnCardClicked(AbilityId id) => AbilitySelected?.Invoke(id);

    private void OnEquipClicked(UniversalAbilitySlot slot)
    {
        if (_shownAbility.IsValid)
        {
            EquipRequested?.Invoke(_shownAbility, slot);
        }
    }

    private void OnSlotClearRequested(UniversalAbilitySlot slot) => ClearRequested?.Invoke(slot);

    private void Subscribe(Button button, Action handler)
    {
        UnityEngine.Events.UnityAction action = () => handler();
        button.onClick.AddListener(action);
        _unsubscribe.Add(() => button.onClick.RemoveListener(action));
    }

    // Deactivate first: Destroy is deferred in Play Mode and the entry must stop counting immediately.
    private static void Discard<T>(List<T> spawned) where T : Component
    {
        foreach (T item in spawned)
        {
            if (item == null)
            {
                continue;
            }

            item.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(item.gameObject);
            }
            else
            {
                DestroyImmediate(item.gameObject);
            }
        }

        spawned.Clear();
    }

    private static int SlotIndex(UniversalAbilitySlot slot) => slot == UniversalAbilitySlot.Slot2 ? 1 : 0;

    private static UniversalAbilitySlot SlotAt(int index) =>
        index == 1 ? UniversalAbilitySlot.Slot2 : UniversalAbilitySlot.Slot1;
}
