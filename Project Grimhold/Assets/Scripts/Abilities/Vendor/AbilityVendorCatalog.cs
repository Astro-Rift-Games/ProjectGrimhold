using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static configuration of what the Ability Vendor sells and for how much. It is never mutated at
/// runtime: ownership and Currency live in the persisted profile, and the price is read from here
/// at purchase time, never from the view.
/// </summary>
[CreateAssetMenu(fileName = "AbilityVendorCatalog", menuName = "Grimhold/Abilities/Ability Vendor Catalog")]
public sealed class AbilityVendorCatalog : ScriptableObject
{
    [SerializeField]
    private List<AbilityVendorOffer> _offers = new();

    /// <summary>The authored offers in display order.</summary>
    public IReadOnlyList<AbilityVendorOffer> Offers =>
        _offers != null ? _offers : System.Array.Empty<AbilityVendorOffer>();

    public bool TryGetOffer(AbilityId abilityId, out AbilityVendorOffer offer)
    {
        offer = default;
        if (!abilityId.IsValid || _offers == null)
        {
            return false;
        }

        for (int index = 0; index < _offers.Count; index++)
        {
            AbilityDefinition ability = _offers[index].Ability;
            if (ability != null && ability.AbilityId == abilityId)
            {
                offer = _offers[index];
                return true;
            }
        }

        return false;
    }

    public bool TryValidate(AbilityDefinitionCatalog abilityCatalog, out string error)
    {
        if (abilityCatalog == null)
        {
            error = $"Ability vendor catalog '{name}' requires an ability definition catalog to validate against.";
            return false;
        }

        if (_offers == null)
        {
            error = $"Ability vendor catalog '{name}' has a missing offers collection.";
            return false;
        }

        var seenIds = new HashSet<AbilityId>();
        for (int index = 0; index < _offers.Count; index++)
        {
            AbilityVendorOffer offer = _offers[index];
            if (offer.Ability == null)
            {
                error = $"Ability vendor catalog '{name}' has a null ability at offer {index}.";
                return false;
            }

            AbilityId abilityId = offer.Ability.AbilityId;
            if (!abilityId.IsValid)
            {
                error = $"Ability vendor catalog '{name}' offer {index} ('{offer.Ability.name}') has an invalid ability ID.";
                return false;
            }

            if (!abilityCatalog.TryGet(abilityId, out _))
            {
                error = $"Ability vendor catalog '{name}' offer {index} references unknown ability '{abilityId.Value}'.";
                return false;
            }

            if (offer.Price <= 0)
            {
                error = $"Ability vendor catalog '{name}' offer {index} ('{abilityId.Value}') has non-positive price {offer.Price}.";
                return false;
            }

            if (!seenIds.Add(abilityId))
            {
                error = $"Ability vendor catalog '{name}' offers ability '{abilityId.Value}' more than once.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
