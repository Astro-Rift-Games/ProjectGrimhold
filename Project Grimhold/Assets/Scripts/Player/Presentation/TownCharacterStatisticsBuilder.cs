using System;
using System.Collections.Generic;

/// <summary>
/// Pure projection of confirmed attributes and prepared equipment into the Town character sheet.
/// It reuses the calculators the Raid player uses, so Town and Raid numbers match.
/// </summary>
public static class TownCharacterStatisticsBuilder
{
    /// <summary>
    /// Fallback mitigation constant. The authoritative value is the serialized
    /// <c>PlayerCharacter._defenseMitigationConstant</c> (currently 100).
    /// </summary>
    public const float DefaultDefenseMitigationConstant = 100f;

    private static readonly EquipmentSlot[] SheetSlotOrder =
    {
        EquipmentSlot.Helmet, EquipmentSlot.Armor, EquipmentSlot.Gloves, EquipmentSlot.Boots,
        EquipmentSlot.WeaponSetAMainHand, EquipmentSlot.WeaponSetAOffHand,
        EquipmentSlot.WeaponSetBMainHand, EquipmentSlot.WeaponSetBOffHand
    };

    public static bool TryBuild(
        in CharacterAttributeState attributes,
        in PreparedEquipmentLoadout equipment,
        Func<LootId, LootDefinition> resolveLoot,
        in CharacterDerivedStatisticsConfiguration configuration,
        float defenseMitigationConstant,
        out TownCharacterStatisticsPresentation presentation,
        out string failure)
    {
        presentation = default;
        failure = null;

        if (configuration == null)
        {
            failure = "Character-derived-statistics configuration is unavailable.";
            return false;
        }

        if (float.IsNaN(defenseMitigationConstant) || float.IsInfinity(defenseMitigationConstant) ||
            defenseMitigationConstant < 0f)
        {
            failure = "The defense mitigation constant must be finite and non-negative.";
            return false;
        }

        var slots = new TownEquipmentSlotEntry[SheetSlotOrder.Length];
        var definitions = new Dictionary<EquipmentSlot, LootDefinition>(SheetSlotOrder.Length);
        for (int index = 0; index < SheetSlotOrder.Length; index++)
        {
            EquipmentSlot slot = SheetSlotOrder[index];
            LootId lootId = equipment.Get(slot);
            LootDefinition definition = Resolve(resolveLoot, lootId);
            if (definition != null) definitions[slot] = definition;
            slots[index] = new TownEquipmentSlotEntry(
                slot,
                lootId,
                definition != null ? definition.DisplayName : lootId.IsValid ? lootId.Value : string.Empty,
                definition != null ? definition.Icon : null);
        }

        if (!EquipmentStatisticsCalculator.TryCalculate(
                ArmorFor(definitions, EquipmentSlot.Helmet),
                ArmorFor(definitions, EquipmentSlot.Armor),
                ArmorFor(definitions, EquipmentSlot.Gloves),
                ArmorFor(definitions, EquipmentSlot.Boots),
                out EquipmentStatisticsModifiers modifiers,
                out EquipmentStatisticsCalculationFailure equipmentFailure))
        {
            failure = $"Equipment statistics could not be calculated: {equipmentFailure}.";
            return false;
        }

        if (!CharacterDerivedStatisticsCalculator.TryCalculate(
                attributes,
                configuration,
                out CharacterDerivedStatistics attributeStatistics,
                out CharacterDerivedStatisticsCalculationFailure attributeFailure))
        {
            failure = $"Character statistics could not be calculated: {attributeFailure}.";
            return false;
        }

        if (!PlayerRuntimeStatisticsCalculator.TryCalculate(
                attributes,
                configuration,
                modifiers,
                out PlayerRuntimeStatistics runtimeStatistics,
                out CharacterDerivedStatisticsCalculationFailure runtimeFailure))
        {
            failure = $"Character statistics with equipment could not be calculated: {runtimeFailure}.";
            return false;
        }

        presentation = new TownCharacterStatisticsPresentation(
            new TownStatBreakdown(
                attributeStatistics.MaximumHealth,
                runtimeStatistics.MaximumHealth - attributeStatistics.MaximumHealth),
            new TownStatBreakdown(
                attributeStatistics.MaximumStamina,
                runtimeStatistics.MaximumStamina - attributeStatistics.MaximumStamina),
            new TownStatBreakdown(
                attributeStatistics.MaximumMana,
                runtimeStatistics.MaximumMana - attributeStatistics.MaximumMana),
            runtimeStatistics.PhysicalDefense,
            runtimeStatistics.MagicalDefense,
            MitigationPercent(runtimeStatistics.PhysicalDefense, defenseMitigationConstant),
            MitigationPercent(runtimeStatistics.MagicalDefense, defenseMitigationConstant),
            runtimeStatistics.AdditionalLootChanceBasisPoints / 100f,
            BuildWeapons(attributes, equipment, definitions),
            slots);
        return true;
    }

    private static LootDefinition Resolve(Func<LootId, LootDefinition> resolveLoot, LootId lootId) =>
        resolveLoot != null && lootId.IsValid ? resolveLoot(lootId) : null;

    private static ArmorDefinition ArmorFor(
        Dictionary<EquipmentSlot, LootDefinition> definitions,
        EquipmentSlot slot) =>
        definitions.TryGetValue(slot, out LootDefinition definition) &&
        EquipmentSlotRules.IsCompatible(definition.Category, slot)
            ? definition.ArmorDefinition
            : null;

    private static float MitigationPercent(int defense, float constant)
    {
        if (defense <= 0) return 0f;
        return defense / (defense + constant) * 100f;
    }

    private static TownWeaponStatistics[] BuildWeapons(
        in CharacterAttributeState attributes,
        in PreparedEquipmentLoadout equipment,
        Dictionary<EquipmentSlot, LootDefinition> definitions)
    {
        var weapons = new List<TownWeaponStatistics>(EquipmentSlotRules.HandSlots.Length);
        EquipmentSlot[] handSlots = EquipmentSlotRules.HandSlots;
        for (int index = 0; index < handSlots.Length; index++)
        {
            EquipmentSlot slot = handSlots[index];
            if (!definitions.TryGetValue(slot, out LootDefinition definition) ||
                definition.Category != LootCategory.Weapon ||
                definition.WeaponDefinition == null ||
                !EquipmentSlotRules.IsCompatible(definition.WeaponDefinition, slot))
            {
                continue;
            }

            if (EquipmentSlotRules.IsOffHandSlot(slot) &&
                IsTwoHanded(definitions, EquipmentSlotRules.GetMainHandSlot(slot)))
            {
                continue;
            }

            WeaponDefinition weapon = definition.WeaponDefinition;
            // Same pure resolution path as PlayerWeaponEquipmentNetworkController.TryResolveEffectiveDamage.
            float effectiveDamage =
                WeaponScalingContributionsResolver.TryResolve(
                    weapon.OffensiveScaling,
                    out WeaponScalingContributions contributions) &&
                WeaponDamageCalculator.TryCalculate(
                    weapon.BaseDamage,
                    attributes,
                    contributions,
                    out float calculated)
                    ? calculated
                    : 0f;
            weapons.Add(new TownWeaponStatistics(
                slot,
                equipment.Get(slot),
                definition.DisplayName,
                weapon.DamageType,
                weapon.BaseDamage,
                effectiveDamage));
        }

        return weapons.ToArray();
    }

    private static bool IsTwoHanded(
        Dictionary<EquipmentSlot, LootDefinition> definitions,
        EquipmentSlot mainHand) =>
        definitions.TryGetValue(mainHand, out LootDefinition main) &&
        main.Category == LootCategory.Weapon && main.WeaponDefinition != null &&
        main.WeaponDefinition.Handedness == WeaponHandedness.TwoHanded;
}
