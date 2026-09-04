using System.Collections.Generic;

/// <summary>
/// Non-owning Equipment assignments over units that remain in the Town Loadout. It covers the six
/// slots of <see cref="EquipmentSlot"/>: the two weapon quick slots plus Helmet, Armor, Gloves and
/// Boots. Assigning never moves or consumes a unit; the Loadout stays the owner until the raid
/// reservation transfers it.
/// </summary>
public readonly struct PreparedEquipmentLoadout
{
    public LootId WeaponSlot1 { get; }
    public LootId WeaponSlot2 { get; }
    public LootId Helmet { get; }
    public LootId Armor { get; }
    public LootId Gloves { get; }
    public LootId Boots { get; }

    public bool HasWeaponSlot1 => WeaponSlot1.IsValid;
    public bool HasWeaponSlot2 => WeaponSlot2.IsValid;
    public bool HasAnyWeapon => HasWeaponSlot1 || HasWeaponSlot2;

    /// <summary>True when any of the six slots holds an assignment.</summary>
    public bool HasAnyEquipment
    {
        get
        {
            EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
            for (int index = 0; index < slots.Length; index++)
            {
                if (Get(slots[index]).IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public PreparedEquipmentLoadout(
        LootId weaponSlot1,
        LootId weaponSlot2,
        LootId helmet = default,
        LootId armor = default,
        LootId gloves = default,
        LootId boots = default)
    {
        WeaponSlot1 = weaponSlot1;
        WeaponSlot2 = weaponSlot2;
        Helmet = helmet;
        Armor = armor;
        Gloves = gloves;
        Boots = boots;
    }

    public LootId Get(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.WeaponSlot1 => WeaponSlot1,
        EquipmentSlot.WeaponSlot2 => WeaponSlot2,
        EquipmentSlot.Helmet => Helmet,
        EquipmentSlot.Armor => Armor,
        EquipmentSlot.Gloves => Gloves,
        EquipmentSlot.Boots => Boots,
        _ => default
    };

    public LootId Get(WeaponSlot slot) => Get(EquipmentSlotRules.FromWeaponSlot(slot));

    public PreparedEquipmentLoadout With(EquipmentSlot slot, LootId lootId) => slot switch
    {
        EquipmentSlot.WeaponSlot1 => new PreparedEquipmentLoadout(lootId, WeaponSlot2, Helmet, Armor, Gloves, Boots),
        EquipmentSlot.WeaponSlot2 => new PreparedEquipmentLoadout(WeaponSlot1, lootId, Helmet, Armor, Gloves, Boots),
        EquipmentSlot.Helmet => new PreparedEquipmentLoadout(WeaponSlot1, WeaponSlot2, lootId, Armor, Gloves, Boots),
        EquipmentSlot.Armor => new PreparedEquipmentLoadout(WeaponSlot1, WeaponSlot2, Helmet, lootId, Gloves, Boots),
        EquipmentSlot.Gloves => new PreparedEquipmentLoadout(WeaponSlot1, WeaponSlot2, Helmet, Armor, lootId, Boots),
        EquipmentSlot.Boots => new PreparedEquipmentLoadout(WeaponSlot1, WeaponSlot2, Helmet, Armor, Gloves, lootId),
        _ => this
    };

    public PreparedEquipmentLoadout Without(EquipmentSlot slot) => With(slot, default);

    /// <summary>
    /// Validates every occupied slot against slot compatibility, catalog usability and the units
    /// owned by <paramref name="ownedItems"/>. A unit referenced by several slots requires one
    /// owned unit per reference.
    /// </summary>
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

        if (requireWeapon && !loadout.HasAnyWeapon)
        {
            error = "At least one prepared weapon is required.";
            return false;
        }

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        for (int index = 0; index < slots.Length; index++)
        {
            EquipmentSlot slot = slots[index];
            LootId lootId = loadout.Get(slot);
            if (!lootId.IsValid)
            {
                continue;
            }

            if (!IsUsableEquipmentDefinition(lootId, slot, catalog))
            {
                error = $"Prepared '{lootId.Value}' cannot occupy {slot}.";
                return false;
            }

        }

        return true;
    }

    /// <summary>
    /// Resolves whether a loot identity may occupy <paramref name="slot"/>, independently from
    /// ownership. Weapon slots additionally require a usable weapon definition.
    /// </summary>
    public static bool IsUsableEquipmentDefinition(
        LootId lootId,
        EquipmentSlot slot,
        LootDefinitionCatalog catalog)
    {
        if (!lootId.IsValid || catalog == null || !EquipmentSlotRules.IsEquipmentSlot(slot) ||
            !catalog.TryGet(lootId.Value, out LootDefinition definition) ||
            !EquipmentSlotRules.IsCompatible(definition.Category, slot))
        {
            return false;
        }

        return !EquipmentSlotRules.IsWeaponSlot(slot) || IsUsableWeaponDefinition(lootId, catalog);
    }

    /// <summary>
    /// Resolves whether a loot identity is a usable weapon in the catalog, independently from
    /// ownership. Preparation uses it to validate a configured recovery weapon before granting it.
    /// </summary>
    public static bool IsUsableWeaponDefinition(LootId lootId, LootDefinitionCatalog catalog)
    {
        return lootId.IsValid && catalog != null &&
            catalog.TryGet(lootId.Value, out LootDefinition definition) &&
            definition.Category == LootCategory.Weapon && definition.WeaponDefinition != null &&
            definition.WeaponDefinition.TryValidate(out _);
    }

    /// <summary>
    /// Evaluates every prepared weapon against the same confirmed character-attribute state.
    /// Armor slots are deliberately outside the MVP requirement contract.
    /// </summary>
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

        EquipmentSlot[] weaponSlots =
        {
            EquipmentSlot.WeaponSlot1,
            EquipmentSlot.WeaponSlot2
        };
        for (int index = 0; index < weaponSlots.Length; index++)
        {
            LootId lootId = loadout.Get(weaponSlots[index]);
            if (!lootId.IsValid)
            {
                continue;
            }

            if (!catalog.TryGet(lootId.Value, out LootDefinition definition) ||
                definition == null || definition.WeaponDefinition == null)
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


}
