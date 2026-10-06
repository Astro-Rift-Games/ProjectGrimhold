using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One unlocked ability card of the Town Abilities list.</summary>
[DisallowMultipleComponent]
public sealed class TownAbilityCardView : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private GameObject _iconPlaceholder;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _detailText;
    [SerializeField] private TMP_Text _stateText;
    [SerializeField] private GameObject _selectionHighlight;

    public AbilityId Id { get; private set; }
    public Button Button => _button;
    public TMP_Text NameText => _nameText;
    public TMP_Text DetailText => _detailText;
    public TMP_Text StateText => _stateText;
    public bool IsSelected => _selectionHighlight != null && _selectionHighlight.activeSelf;

    public event Action<AbilityId> Clicked;

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

    public void Present(in TownAbilityEntry entry, bool selected)
    {
        Id = entry.Id;
        _nameText.text = entry.DisplayName;
        _detailText.text = TownAbilityText.CardSubtitle(entry);
        _stateText.text = TownAbilityText.StateBadge(entry.State);
        TownAbilityIconUtility.Apply(_icon, _iconPlaceholder, entry.Icon);
        if (_selectionHighlight != null)
        {
            _selectionHighlight.SetActive(selected);
        }
    }

    private void OnClicked() => Clicked?.Invoke(Id);
}
