using System.Globalization;
using System.Text;

/// <summary>Pure English text formatting for the Ability Vendor panel.</summary>
public static class AbilityVendorText
{
    private const string NoRequirement = "None";

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
}
