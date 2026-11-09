using System.Collections.Generic;

/// <summary>
/// Doc 09 §15.2: on Town return the training weapon goes back to the Trainer once the character
/// owns a normal weapon anywhere (Loadout, Stash or prepared Equipment); otherwise it is kept so
/// the character is never left without a weapon. Attribute requirements do not matter here.
/// </summary>
public static class TrainingWeaponReturnRule
{
    /// <summary>Removes every training weapon when a normal weapon is owned. Returns whether anything was removed.</summary>
    public static bool TryApply(
        List<StashItem> loadout,
        List<StashItem> stash,
        ref PreparedEquipmentLoadout equipment,
        LootDefinitionCatalog catalog)
    {
        if (catalog == null || !OwnsNormalWeapon(loadout, stash, equipment, catalog))
            return false;

        bool removed = loadout.RemoveAll(item => IsTrainingWeapon(item.LootId, catalog)) > 0;
        removed |= stash.RemoveAll(item => IsTrainingWeapon(item.LootId, catalog)) > 0;
        foreach (EquipmentSlot slot in EquipmentSlotRules.AllSlots)
        {
            if (!IsTrainingWeapon(equipment.Get(slot), catalog)) continue;
            equipment = equipment.Without(slot);
            removed = true;
        }

        return removed;
    }

    private static bool OwnsNormalWeapon(
        List<StashItem> loadout,
        List<StashItem> stash,
        in PreparedEquipmentLoadout equipment,
        LootDefinitionCatalog catalog)
    {
        foreach (StashItem item in loadout)
            if (IsNormalWeapon(item.LootId, catalog)) return true;
        foreach (StashItem item in stash)
            if (IsNormalWeapon(item.LootId, catalog)) return true;
        foreach (EquipmentSlot slot in EquipmentSlotRules.AllSlots)
            if (IsNormalWeapon(equipment.Get(slot), catalog)) return true;
        return false;
    }

    private static bool IsNormalWeapon(LootId lootId, LootDefinitionCatalog catalog) =>
        TryResolveWeapon(lootId, catalog, out LootDefinition definition) && !definition.IsTrainingWeapon;

    private static bool IsTrainingWeapon(LootId lootId, LootDefinitionCatalog catalog) =>
        TryResolveWeapon(lootId, catalog, out LootDefinition definition) && definition.IsTrainingWeapon;

    private static bool TryResolveWeapon(LootId lootId, LootDefinitionCatalog catalog, out LootDefinition definition)
    {
        definition = null;
        return lootId.IsValid && catalog.TryGet(lootId.Value, out definition) &&
               definition != null && definition.Category == LootCategory.Weapon;
    }
}
