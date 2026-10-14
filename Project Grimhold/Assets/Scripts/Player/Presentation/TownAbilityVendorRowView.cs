using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One offer row of the Ability Vendor panel.</summary>
[DisallowMultipleComponent]
public sealed class TownAbilityVendorRowView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private GameObject _iconPlaceholder;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _requirementText;
    [SerializeField] private TMP_Text _priceText;
    [SerializeField] private TMP_Text _stateText;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TMP_Text _buyLabel;
    [SerializeField] private Color _requirementMetColor = new(0.45f, 0.78f, 0.4f, 1f);
    [SerializeField] private Color _requirementUnmetColor = new(0.85f, 0.3f, 0.25f, 1f);

    private AbilityId _abilityId;

    public AbilityId AbilityId => _abilityId;
    public Button BuyButton => _buyButton;
    public TMP_Text NameText => _nameText;
    public TMP_Text RequirementText => _requirementText;
    public TMP_Text PriceText => _priceText;
    public TMP_Text StateText => _stateText;
    public TMP_Text BuyLabel => _buyLabel;

    public event Action<AbilityId> BuyRequested;

    /// <summary>Reports every mandatory serialized reference that is missing.</summary>
    public bool TryValidate(out string error)
    {
        if (_nameText == null) return Fail(nameof(_nameText), out error);
        if (_requirementText == null) return Fail(nameof(_requirementText), out error);
        if (_priceText == null) return Fail(nameof(_priceText), out error);
        if (_stateText == null) return Fail(nameof(_stateText), out error);
        if (_buyButton == null) return Fail(nameof(_buyButton), out error);
        if (_buyLabel == null) return Fail(nameof(_buyLabel), out error);

        error = null;
        return true;
    }

    private void Awake()
    {
        if (_buyButton != null)
        {
            _buyButton.onClick.AddListener(OnBuyClicked);
        }
    }

    private void OnDestroy()
    {
        if (_buyButton != null)
        {
            _buyButton.onClick.RemoveListener(OnBuyClicked);
        }
    }

    public void Present(in AbilityVendorEntry entry)
    {
        AbilityDefinition definition = entry.Definition;
        _abilityId = entry.AbilityId;
        _nameText.text = definition != null ? definition.DisplayName : entry.AbilityId.ToString();
        if (_descriptionText != null)
        {
            _descriptionText.text = definition != null ? definition.Description : string.Empty;
        }

        // Requirements are informational: an unmet one is marked but never blocks the purchase.
        _requirementText.text = $"Req: {AbilityVendorText.Requirement(definition)}";
        _requirementText.color = entry.RequirementsMet ? _requirementMetColor : _requirementUnmetColor;
        _priceText.text = AbilityVendorText.Price(entry.Price);
        _stateText.text = AbilityVendorText.StateLabel(entry.State);
        _buyLabel.text = AbilityVendorText.BuyLabel(entry.State, entry.Price);
        _buyButton.interactable = entry.CanBuy;
        TownAbilityIconUtility.Apply(_icon, _iconPlaceholder, definition != null ? definition.Icon : null);
    }

    private void OnBuyClicked() => BuyRequested?.Invoke(_abilityId);

    private bool Fail(string fieldName, out string error)
    {
        error = $"{nameof(TownAbilityVendorRowView)} is missing serialized reference '{fieldName}'.";
        return false;
    }
}
