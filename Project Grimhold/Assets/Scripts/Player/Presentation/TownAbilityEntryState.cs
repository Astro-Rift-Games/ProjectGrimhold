/// <summary>How an unlocked ability is presented in the Town Abilities tab.</summary>
public enum TownAbilityEntryState
{
    /// <summary>Unlocked, requirements met and not prepared in any slot.</summary>
    Available,

    /// <summary>Prepared in one of the two universal slots.</summary>
    Equipped,

    /// <summary>Unlocked, but at least one attribute requirement is not met.</summary>
    RequirementsNotMet
}
