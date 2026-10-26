/// <summary>Advisory equipment eligibility only; the backend owns acceptance.</summary>
public sealed class EquipmentBuybackEligibilityPolicy : IBuybackEligibilityPolicy
{
    public bool IsEligible(LootDefinition definition) => definition != null &&
        definition.Category is LootCategory.Weapon or LootCategory.Helmet or LootCategory.Armor or
            LootCategory.Gloves or LootCategory.Boots or LootCategory.Shield;
}
