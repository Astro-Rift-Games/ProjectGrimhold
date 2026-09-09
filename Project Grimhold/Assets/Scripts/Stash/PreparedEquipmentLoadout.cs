/// <summary>Exclusive Equipment assignments for two Weapon Sets and four armor slots.</summary>
public readonly struct PreparedEquipmentLoadout
{
    public LootId WeaponSetAMainHand { get; }
    public LootId WeaponSetAOffHand { get; }
    public LootId WeaponSetBMainHand { get; }
    public LootId WeaponSetBOffHand { get; }
    public LootId Helmet { get; }
    public LootId Armor { get; }
    public LootId Gloves { get; }
    public LootId Boots { get; }

    public bool HasAnyMainHand => WeaponSetAMainHand.IsValid || WeaponSetBMainHand.IsValid;
    public bool HasWeaponSetAMainHand => WeaponSetAMainHand.IsValid;
    public bool HasWeaponSetBMainHand => WeaponSetBMainHand.IsValid;
    public bool HasAnyWeapon => HasAnyMainHand || WeaponSetAOffHand.IsValid || WeaponSetBOffHand.IsValid;

    public bool HasAnyEquipment
    {
        get
        {
            EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
            for (int index = 0; index < slots.Length; index++)
            {
                if (Get(slots[index]).IsValid) return true;
            }

            return false;
        }
    }

    // The first six parameters retain the historical serialized constructor order. New callers
    // should use named arguments for the two Off Hand values.
    public PreparedEquipmentLoadout(
        LootId weaponSetAMainHand,
        LootId weaponSetBMainHand,
        LootId helmet = default,
        LootId armor = default,
        LootId gloves = default,
        LootId boots = default,
        LootId weaponSetAOffHand = default,
        LootId weaponSetBOffHand = default)
    {
        WeaponSetAMainHand = weaponSetAMainHand;
        WeaponSetAOffHand = weaponSetAOffHand;
        WeaponSetBMainHand = weaponSetBMainHand;
        WeaponSetBOffHand = weaponSetBOffHand;
        Helmet = helmet;
        Armor = armor;
        Gloves = gloves;
        Boots = boots;
    }

    public LootId Get(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.WeaponSetAMainHand => WeaponSetAMainHand,
        EquipmentSlot.WeaponSetAOffHand => WeaponSetAOffHand,
        EquipmentSlot.WeaponSetBMainHand => WeaponSetBMainHand,
        EquipmentSlot.WeaponSetBOffHand => WeaponSetBOffHand,
        EquipmentSlot.Helmet => Helmet,
        EquipmentSlot.Armor => Armor,
        EquipmentSlot.Gloves => Gloves,
        EquipmentSlot.Boots => Boots,
        _ => default
    };

    public PreparedEquipmentLoadout With(EquipmentSlot slot, LootId lootId) => new(
        slot == EquipmentSlot.WeaponSetAMainHand ? lootId : WeaponSetAMainHand,
        slot == EquipmentSlot.WeaponSetBMainHand ? lootId : WeaponSetBMainHand,
        slot == EquipmentSlot.Helmet ? lootId : Helmet,
        slot == EquipmentSlot.Armor ? lootId : Armor,
        slot == EquipmentSlot.Gloves ? lootId : Gloves,
        slot == EquipmentSlot.Boots ? lootId : Boots,
        slot == EquipmentSlot.WeaponSetAOffHand ? lootId : WeaponSetAOffHand,
        slot == EquipmentSlot.WeaponSetBOffHand ? lootId : WeaponSetBOffHand);

    public PreparedEquipmentLoadout Without(EquipmentSlot slot) => With(slot, default);

    public static bool TryValidate(
        in PreparedEquipmentLoadout loadout,
        LootDefinitionCatalog catalog,
        bool requireWeapon,
        out string error)
    {
        error = null;
        if (catalog == null)
        {
            error = "Prepared equipment validation dependencies are unavailable.";
            return false;
        }

        if (requireWeapon && !loadout.HasAnyMainHand)
        {
            error = "At least one prepared Main Hand weapon is required.";
            return false;
        }

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        for (int index = 0; index < slots.Length; index++)
        {
            EquipmentSlot slot = slots[index];
            LootId lootId = loadout.Get(slot);
            if (lootId.IsValid && !IsUsableEquipmentDefinition(lootId, slot, catalog))
            {
                error = $"Prepared '{lootId.Value}' cannot occupy {slot}.";
                return false;
            }
        }

        if (!TryValidateSet(loadout, WeaponSetSlot.SetA, catalog, out error) ||
            !TryValidateSet(loadout, WeaponSetSlot.SetB, catalog, out error))
        {
            return false;
        }

        return true;
    }

    public static bool IsUsableEquipmentDefinition(
        LootId lootId,
        EquipmentSlot slot,
        LootDefinitionCatalog catalog)
    {
        if (!lootId.IsValid || catalog == null || !EquipmentSlotRules.IsEquipmentSlot(slot) ||
            !catalog.TryGet(lootId.Value, out LootDefinition definition) || definition == null ||
            !EquipmentSlotRules.IsCompatible(definition.Category, slot))
        {
            return false;
        }

        return !EquipmentSlotRules.IsHandSlot(slot) ||
            definition.WeaponDefinition != null && definition.WeaponDefinition.TryValidate(out _) &&
            EquipmentSlotRules.IsCompatible(definition.WeaponDefinition, slot);
    }

    public static bool IsUsableWeaponDefinition(LootId lootId, LootDefinitionCatalog catalog) =>
        lootId.IsValid && catalog != null &&
        catalog.TryGet(lootId.Value, out LootDefinition definition) && definition != null &&
        definition.Category == LootCategory.Weapon && definition.WeaponDefinition != null &&
        definition.WeaponDefinition.TryValidate(out _);

    public static bool TryValidateWeaponRequirements(
        in PreparedEquipmentLoadout loadout,
        in CharacterAttributeState attributes,
        LootDefinitionCatalog catalog,
        out string error)
    {
        error = null;
        if (catalog == null)
        {
            error = "Prepared weapon requirement validation needs a loot catalog.";
            return false;
        }

        EquipmentSlot[] slots = EquipmentSlotRules.HandSlots;
        for (int index = 0; index < slots.Length; index++)
        {
            LootId lootId = loadout.Get(slots[index]);
            if (!lootId.IsValid) continue;
            if (!catalog.TryGet(lootId.Value, out LootDefinition definition) || definition == null ||
                definition.WeaponDefinition == null)
            {
                error = $"Prepared weapon '{lootId.Value}' cannot be resolved.";
                return false;
            }

            if (!definition.WeaponDefinition.AreAttributeRequirementsSatisfiedBy(attributes))
            {
                error = $"Character attributes do not satisfy prepared weapon '{lootId.Value}'.";
                return false;
            }
        }

        return true;
    }

    public static bool IsOffHandBlocked(
        in PreparedEquipmentLoadout loadout,
        WeaponSetSlot set,
        LootDefinitionCatalog catalog)
    {
        LootId main = loadout.Get(EquipmentSlotRules.GetMainHandSlot(set));
        return TryGetWeapon(main, catalog, out WeaponDefinition weapon) &&
            weapon.Handedness == WeaponHandedness.TwoHanded;
    }

    private static bool TryValidateSet(
        in PreparedEquipmentLoadout loadout,
        WeaponSetSlot set,
        LootDefinitionCatalog catalog,
        out string error)
    {
        error = null;
        if (!IsOffHandBlocked(loadout, set, catalog)) return true;
        EquipmentSlot offHand = EquipmentSlotRules.GetOffHandSlot(set);
        if (!loadout.Get(offHand).IsValid) return true;
        error = $"{offHand} is blocked by a two-handed Main Hand weapon.";
        return false;
    }

    private static bool TryGetWeapon(
        LootId lootId,
        LootDefinitionCatalog catalog,
        out WeaponDefinition weapon)
    {
        weapon = null;
        if (!lootId.IsValid || catalog == null || !catalog.TryGet(lootId.Value, out LootDefinition definition) ||
            definition == null || definition.Category != LootCategory.Weapon)
        {
            return false;
        }

        weapon = definition.WeaponDefinition;
        return weapon != null;
    }
}
