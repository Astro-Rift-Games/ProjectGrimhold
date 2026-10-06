using System.Globalization;
using System.Text;

/// <summary>Pure English text formatting for the Town Abilities tab.</summary>
public static class TownAbilityText
{
    private const string NoRequirement = "None";

    public static string AttributeCode(CharacterAttribute attribute) => attribute switch
    {
        CharacterAttribute.Vitality => "VIT",
        CharacterAttribute.Resistance => "RES",
        CharacterAttribute.Strength => "STR",
        CharacterAttribute.Dexterity => "DEX",
        CharacterAttribute.Intelligence => "INT",
        CharacterAttribute.Luck => "LUCK",
        _ => attribute.ToString()
    };

    /// <summary>The attribute requirements joined with " + ", for example "VIT 7 + INT 7".</summary>
    public static string Requirement(in TownAbilityEntry entry)
    {
        var requirements = entry.Requirements;
        if (requirements.Count == 0)
        {
            return NoRequirement;
        }

        var text = new StringBuilder();
        for (int index = 0; index < requirements.Count; index++)
        {
            if (index > 0)
            {
                text.Append(" + ");
            }

            text.Append(AttributeCode(requirements[index].Attribute))
                .Append(' ')
                .Append(requirements[index].Required.ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    public static string RequirementLabel(in TownAbilityRequirementCheck check, string abilityName) =>
        $"{AttributeCode(check.Attribute)} ({abilityName})";

    public static string CurrentOverRequired(in TownAbilityRequirementCheck check) =>
        $"{check.Current.ToString(CultureInfo.InvariantCulture)} / {check.Required.ToString(CultureInfo.InvariantCulture)}";

    public static string Resource(AbilityResourceType resource) => resource switch
    {
        AbilityResourceType.Stamina => "Stamina",
        AbilityResourceType.Mana => "Mana",
        _ => "None"
    };

    /// <summary>Card line, for example "Stamina | DEX 10".</summary>
    public static string CardSubtitle(in TownAbilityEntry entry) =>
        $"{Resource(entry.Resource)} | {Requirement(entry)}";

    /// <summary>Details row, for example "Stamina | 15".</summary>
    public static string ResourceCost(in TownAbilityEntry entry) =>
        $"{Resource(entry.Resource)} | {entry.Cost.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Long cooldown, for example "6 seconds". Trailing zeros are dropped.</summary>
    public static string Cooldown(float seconds)
    {
        string number = seconds.ToString("0.##", CultureInfo.InvariantCulture);
        return number == "1" ? "1 second" : $"{number} seconds";
    }

    /// <summary>Slot panel line, for example "Stamina | CD: 6s".</summary>
    public static string SlotDetail(in TownAbilityEntry entry) =>
        $"{Resource(entry.Resource)} | CD: {entry.CooldownSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s";

    public static string StateBadge(TownAbilityEntryState state) => state switch
    {
        TownAbilityEntryState.Equipped => "Equipped",
        TownAbilityEntryState.RequirementsNotMet => "Missing Req",
        _ => "Unlocked"
    };

    public static string EquipLabel(UniversalAbilitySlot slot, bool alreadyInThisSlot)
    {
        int number = slot == UniversalAbilitySlot.Slot2 ? 2 : 1;
        return alreadyInThisSlot ? $"Equipped in Slot {number}" : $"Equip to Slot {number}";
    }
}
