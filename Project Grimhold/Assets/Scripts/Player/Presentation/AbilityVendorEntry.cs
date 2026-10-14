/// <summary>
/// One Ability Vendor offer as the Town panel shows it. <see cref="RequirementsMet"/> is informational
/// only: attribute requirements gate equipping and using an ability, never acquiring it.
/// </summary>
public readonly struct AbilityVendorEntry
{
    public AbilityId AbilityId { get; }
    public AbilityDefinition Definition { get; }
    public long Price { get; }
    public AbilityVendorOfferState State { get; }
    public bool RequirementsMet { get; }

    public bool CanBuy => State == AbilityVendorOfferState.Available;

    public AbilityVendorEntry(
        AbilityId abilityId,
        AbilityDefinition definition,
        long price,
        AbilityVendorOfferState state,
        bool requirementsMet)
    {
        AbilityId = abilityId;
        Definition = definition;
        Price = price;
        State = state;
        RequirementsMet = requirementsMet;
    }
}
