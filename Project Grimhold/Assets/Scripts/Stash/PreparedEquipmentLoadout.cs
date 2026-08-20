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
        IReadOnlyList<StashItem> ownedItems,
        LootDefinitionCatalog catalog,
        bool requireWeapon,
        out string error)
    {
        error = null;
        if (ownedItems == null || catalog == null)
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

            int required = CountReferences(loadout, lootId);
            if (FindAmount(ownedItems, lootId) < required)
            {
                error = required > 1
                    ? $"Prepared '{lootId.Value}' occupies {required} slots but fewer units are owned."
                    : $"Prepared '{lootId.Value}' is not present in the Loadout.";
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

    /// <summary>Counts how many slots reference the same loot identity.</summary>
    public static int CountReferences(in PreparedEquipmentLoadout loadout, LootId lootId)
    {
        if (!lootId.IsValid)
        {
            return 0;
        }

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        int count = 0;
        for (int index = 0; index < slots.Length; index++)
        {
            if (loadout.Get(slots[index]) == lootId)
            {
                count++;
            }
        }

        return count;
    }

    private static int FindAmount(IReadOnlyList<StashItem> items, LootId lootId)
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (items[index].LootId == lootId)
            {
                return items[index].Amount;
            }
        }

        return 0;
    }
}
