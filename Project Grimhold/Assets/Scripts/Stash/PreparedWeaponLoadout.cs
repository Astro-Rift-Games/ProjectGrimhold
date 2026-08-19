using System.Collections.Generic;

/// <summary>
/// Two non-owning weapon assignments over units that remain in the Town Loadout.
/// </summary>
public readonly struct PreparedWeaponLoadout
{
    public LootId WeaponSlot1 { get; }
    public LootId WeaponSlot2 { get; }

    public bool HasWeaponSlot1 => WeaponSlot1.IsValid;
    public bool HasWeaponSlot2 => WeaponSlot2.IsValid;
    public bool HasAnyWeapon => HasWeaponSlot1 || HasWeaponSlot2;

    public PreparedWeaponLoadout(LootId weaponSlot1, LootId weaponSlot2)
    {
        WeaponSlot1 = weaponSlot1;
        WeaponSlot2 = weaponSlot2;
    }

    public LootId Get(WeaponSlot slot) => slot switch
    {
        WeaponSlot.Slot1 => WeaponSlot1,
        WeaponSlot.Slot2 => WeaponSlot2,
        _ => default
    };

    public PreparedWeaponLoadout With(WeaponSlot slot, LootId lootId) => slot switch
    {
        WeaponSlot.Slot1 => new PreparedWeaponLoadout(lootId, WeaponSlot2),
        WeaponSlot.Slot2 => new PreparedWeaponLoadout(WeaponSlot1, lootId),
        _ => this
    };

    public PreparedWeaponLoadout Without(WeaponSlot slot) => With(slot, default);

    public static bool TryValidate(
        in PreparedWeaponLoadout loadout,
        IReadOnlyList<StashItem> ownedItems,
        LootDefinitionCatalog catalog,
        bool requireWeapon,
        out string error)
    {
        error = null;
        if (ownedItems == null || catalog == null)
        {
            error = "Prepared weapon validation dependencies are unavailable.";
            return false;
        }

        if (requireWeapon && !loadout.HasAnyWeapon)
        {
            error = "At least one prepared weapon is required.";
            return false;
        }

        if (!TryValidateSlot(loadout.WeaponSlot1, ownedItems, catalog, out error) ||
            !TryValidateSlot(loadout.WeaponSlot2, ownedItems, catalog, out error))
        {
            return false;
        }

        if (loadout.HasWeaponSlot1 && loadout.WeaponSlot1 == loadout.WeaponSlot2 &&
            FindAmount(ownedItems, loadout.WeaponSlot1) < 2)
        {
            error = "The same weapon can occupy both slots only when two units are owned.";
            return false;
        }

        return true;
    }

    private static bool TryValidateSlot(
        LootId lootId,
        IReadOnlyList<StashItem> ownedItems,
        LootDefinitionCatalog catalog,
        out string error)
    {
        error = null;
        if (!lootId.IsValid)
        {
            return true;
        }

        if (FindAmount(ownedItems, lootId) <= 0)
        {
            error = $"Prepared weapon '{lootId.Value}' is not present in the Loadout.";
            return false;
        }

        if (!IsUsableWeaponDefinition(lootId, catalog))
        {
            error = $"Prepared weapon '{lootId.Value}' has no valid weapon definition.";
            return false;
        }

        return true;
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
