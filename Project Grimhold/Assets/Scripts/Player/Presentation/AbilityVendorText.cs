using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>Pure English text formatting for the Ability Vendor panel.</summary>
public static class AbilityVendorText
{
    private const string NoRequirement = "None";
    private const string NoGoldNotice = "Not enough gold for any ability. Sell loot to the Merchant to earn gold.";

    /// <summary>The ability's attribute requirements joined with " + ", for example "VIT 7 + INT 7".</summary>
    public static string Requirement(AbilityDefinition definition)
    {
        CharacterAttributeRequirements requirements = definition != null ? definition.AttributeRequirements : null;
        if (requirements == null || requirements.Requirements.Count == 0)
        {
            return NoRequirement;
        }

        var text = new StringBuilder();
        for (int index = 0; index < requirements.Requirements.Count; index++)
        {
            if (index > 0)
            {
                text.Append(" + ");
            }

            CharacterAttributeRequirement requirement = requirements.Requirements[index];
            text.Append(TownAbilityText.AttributeCode(requirement.Attribute))
                .Append(' ')
                .Append(requirement.MinimumValue.ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    public static string Price(long price) =>
        $"{price.ToString(CultureInfo.InvariantCulture)} gold";

    public static string Gold(long currency) =>
        $"Gold: {currency.ToString(CultureInfo.InvariantCulture)}";

    public static string StateLabel(AbilityVendorOfferState state) => state switch
    {
        AbilityVendorOfferState.AlreadyUnlocked => "Owned",
        AbilityVendorOfferState.InsufficientCurrency => "Not enough gold",
        _ => string.Empty
    };

    /// <summary>The buy button caption: the disabled states say why instead of looking unresponsive.</summary>
    public static string BuyLabel(AbilityVendorOfferState state, long price) => state switch
    {
        AbilityVendorOfferState.AlreadyUnlocked => "Owned",
        AbilityVendorOfferState.InsufficientCurrency => $"Need {price.ToString(CultureInfo.InvariantCulture)}",
        _ => "Buy"
    };

    /// <summary>A panel-level hint when gold is the only thing stopping every remaining purchase, otherwise empty.</summary>
    public static string Notice(IReadOnlyList<AbilityVendorEntry> entries)
    {
        bool anyInsufficient = false;
        for (int index = 0; entries != null && index < entries.Count; index++)
        {
            switch (entries[index].State)
            {
                case AbilityVendorOfferState.Available:
                    return string.Empty;
                case AbilityVendorOfferState.InsufficientCurrency:
                    anyInsufficient = true;
                    break;
            }
        }

        return anyInsufficient ? NoGoldNotice : string.Empty;
    }
}
