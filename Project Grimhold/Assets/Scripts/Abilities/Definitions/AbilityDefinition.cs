using UnityEngine;

/// <summary>Immutable authored configuration for one ability.</summary>
[CreateAssetMenu(fileName = "NewAbilityDefinition", menuName = "Grimhold/Abilities/Ability Definition")]
public sealed class AbilityDefinition : ScriptableObject
{
    [SerializeField]
    private string _id;

    [SerializeField]
    private CharacterAttributeRequirements _attributeRequirements = new();

    [SerializeField]
    private AbilityResourceType _resource;

    [SerializeField]
    private int _cost;

    [SerializeField]
    private float _cooldownSeconds;

    public string Id => _id;
    public AbilityId AbilityId => AbilityId.TryCreate(_id, out AbilityId abilityId) ? abilityId : default;
    public CharacterAttributeRequirements AttributeRequirements => _attributeRequirements;
    public AbilityResourceType Resource => _resource;
    public int Cost => _cost;
    public float CooldownSeconds => _cooldownSeconds;

    public bool AreAttributeRequirementsSatisfiedBy(in CharacterAttributeState attributes) =>
        _attributeRequirements != null && _attributeRequirements.IsSatisfiedBy(attributes);

    public bool TryValidate(out string error)
    {
        if (!AbilityId.TryCreate(_id, out _))
        {
            error = $"Ability definition on asset '{name}' has invalid ID '{_id}'.";
            return false;
        }

        if (_attributeRequirements == null)
        {
            error = $"Ability definition '{_id}' has missing attribute requirements.";
            return false;
        }

        if (!_attributeRequirements.TryValidate(out string requirementError))
        {
            error = $"Ability definition '{_id}' has invalid attribute requirements: {requirementError}";
            return false;
        }

        if (_resource != AbilityResourceType.Stamina && _resource != AbilityResourceType.Mana)
        {
            error = $"Ability definition '{_id}' has invalid resource '{_resource}'.";
            return false;
        }

        if (_cost <= 0)
        {
            error = $"Ability definition '{_id}' has invalid cost '{_cost}'.";
            return false;
        }

        if (float.IsNaN(_cooldownSeconds) || float.IsInfinity(_cooldownSeconds) || _cooldownSeconds <= 0f)
        {
            error = $"Ability definition '{_id}' has invalid cooldown '{_cooldownSeconds}'.";
            return false;
        }

        error = null;
        return true;
    }
}
