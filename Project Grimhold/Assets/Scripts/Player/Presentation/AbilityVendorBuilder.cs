using System.Collections.Generic;

/// <summary>
/// Pure projection of the vendor offers, the owned repertoire, the confirmed attributes and the
/// confirmed Currency into the Ability Vendor panel. Owned offers are never buyable; attribute
/// requirements are reported but never change the state.
/// </summary>
public static class AbilityVendorBuilder
{
    public static IReadOnlyList<AbilityVendorEntry> Build(
        IReadOnlyList<AbilityVendorOffer> offers,
        IReadOnlyList<AbilityId> unlocked,
        CharacterAttributeState attributes,
        long currency)
    {
        var entries = new List<AbilityVendorEntry>();
        if (offers == null)
        {
            return entries;
        }

        for (int index = 0; index < offers.Count; index++)
        {
            AbilityVendorOffer offer = offers[index];
            AbilityDefinition definition = offer.Ability;
            if (definition == null)
            {
                continue;
            }

            AbilityId abilityId = definition.AbilityId;
            AbilityVendorOfferState state = IsOwned(unlocked, abilityId)
                ? AbilityVendorOfferState.AlreadyUnlocked
                : currency < offer.Price
                    ? AbilityVendorOfferState.InsufficientCurrency
                    : AbilityVendorOfferState.Available;

            CharacterAttributeRequirements requirements = definition.AttributeRequirements;
            bool requirementsMet = requirements == null || requirements.IsSatisfiedBy(attributes);

            entries.Add(new AbilityVendorEntry(abilityId, definition, offer.Price, state, requirementsMet));
        }

        return entries;
    }

    private static bool IsOwned(IReadOnlyList<AbilityId> unlocked, AbilityId abilityId)
    {
        if (unlocked == null)
        {
            return false;
        }

        for (int index = 0; index < unlocked.Count; index++)
        {
            if (unlocked[index] == abilityId)
            {
                return true;
            }
        }

        return false;
    }
}
