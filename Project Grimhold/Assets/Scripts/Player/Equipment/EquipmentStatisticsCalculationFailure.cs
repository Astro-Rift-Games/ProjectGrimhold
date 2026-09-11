/// <summary>Reason an equipped-armor aggregate could not be calculated.</summary>
public enum EquipmentStatisticsCalculationFailure : byte
{
    None = 0,
    InvalidArmorDefinition = 1,
    PhysicalDefenseOverflow = 2,
    MagicalDefenseOverflow = 3,
    MaximumHealthModifierOverflow = 4,
    MaximumStaminaModifierOverflow = 5,
    MaximumManaModifierOverflow = 6
}
