/// <summary>Pure full-state projection from equipped armor definitions to effective modifiers.</summary>
public static class EquipmentStatisticsCalculator
{
    public static bool TryCalculate(
        ArmorDefinition helmet,
        ArmorDefinition armor,
        ArmorDefinition gloves,
        ArmorDefinition boots,
        out EquipmentStatisticsModifiers modifiers,
        out EquipmentStatisticsCalculationFailure failure)
    {
        modifiers = default;
        failure = EquipmentStatisticsCalculationFailure.None;

        long physicalDefense = 0;
        long magicalDefense = 0;
        long maximumHealthModifier = 0;
        long maximumStaminaModifier = 0;
        long maximumManaModifier = 0;

        if (!TryAccumulate(helmet, ref physicalDefense, ref magicalDefense,
                ref maximumHealthModifier, ref maximumStaminaModifier, ref maximumManaModifier, out failure) ||
            !TryAccumulate(armor, ref physicalDefense, ref magicalDefense,
                ref maximumHealthModifier, ref maximumStaminaModifier, ref maximumManaModifier, out failure) ||
            !TryAccumulate(gloves, ref physicalDefense, ref magicalDefense,
                ref maximumHealthModifier, ref maximumStaminaModifier, ref maximumManaModifier, out failure) ||
            !TryAccumulate(boots, ref physicalDefense, ref magicalDefense,
                ref maximumHealthModifier, ref maximumStaminaModifier, ref maximumManaModifier, out failure))
        {
            return false;
        }

        if (physicalDefense > int.MaxValue)
        {
            failure = EquipmentStatisticsCalculationFailure.PhysicalDefenseOverflow;
            return false;
        }

        if (magicalDefense > int.MaxValue)
        {
            failure = EquipmentStatisticsCalculationFailure.MagicalDefenseOverflow;
            return false;
        }

        if (maximumHealthModifier > int.MaxValue)
        {
            failure = EquipmentStatisticsCalculationFailure.MaximumHealthModifierOverflow;
            return false;
        }

        if (maximumStaminaModifier > int.MaxValue)
        {
            failure = EquipmentStatisticsCalculationFailure.MaximumStaminaModifierOverflow;
            return false;
        }

        if (maximumManaModifier > int.MaxValue)
        {
            failure = EquipmentStatisticsCalculationFailure.MaximumManaModifierOverflow;
            return false;
        }

        modifiers = new EquipmentStatisticsModifiers(
            (int)physicalDefense,
            (int)magicalDefense,
            (int)maximumHealthModifier,
            (int)maximumStaminaModifier,
            (int)maximumManaModifier);
        return true;
    }

    private static bool TryAccumulate(
        ArmorDefinition definition,
        ref long physicalDefense,
        ref long magicalDefense,
        ref long maximumHealthModifier,
        ref long maximumStaminaModifier,
        ref long maximumManaModifier,
        out EquipmentStatisticsCalculationFailure failure)
    {
        failure = EquipmentStatisticsCalculationFailure.None;
        if (definition == null)
        {
            return true;
        }

        if (!definition.TryValidate(out _))
        {
            failure = EquipmentStatisticsCalculationFailure.InvalidArmorDefinition;
            return false;
        }

        physicalDefense += definition.PhysicalDefense;
        magicalDefense += definition.MagicalDefense;

        MaximumResourceModifier resourceModifier = definition.MaximumResourceModifier;
        switch (resourceModifier.Resource)
        {
            case MaximumResourceType.Health:
                maximumHealthModifier += resourceModifier.Amount;
                break;
            case MaximumResourceType.Stamina:
                maximumStaminaModifier += resourceModifier.Amount;
                break;
            case MaximumResourceType.Mana:
                maximumManaModifier += resourceModifier.Amount;
                break;
        }

        return true;
    }
}
