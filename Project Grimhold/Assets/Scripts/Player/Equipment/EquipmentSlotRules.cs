/// <summary>
/// Single source of truth for Equipment slot compatibility. Loot only classifies a unit through
/// <see cref="LootCategory"/>; deciding which slot may receive it belongs here.
/// </summary>
public static class EquipmentSlotRules
{
    public const int SlotCount = 6;

    /// <summary>Every Equipment slot in a stable presentation and projection order.</summary>
    public static readonly EquipmentSlot[] AllSlots =
    {
        EquipmentSlot.WeaponSlot1, EquipmentSlot.WeaponSlot2, EquipmentSlot.Helmet,
        EquipmentSlot.Armor, EquipmentSlot.Gloves, EquipmentSlot.Boots
    };

    public static bool IsWeaponSlot(EquipmentSlot slot) =>
        slot == EquipmentSlot.WeaponSlot1 || slot == EquipmentSlot.WeaponSlot2;

    public static bool IsArmorSlot(EquipmentSlot slot) =>
        slot == EquipmentSlot.Helmet || slot == EquipmentSlot.Armor ||
        slot == EquipmentSlot.Gloves || slot == EquipmentSlot.Boots;

    public static bool IsEquipmentSlot(EquipmentSlot slot) => IsWeaponSlot(slot) || IsArmorSlot(slot);

    public static bool IsEquippableCategory(LootCategory category) =>
        category == LootCategory.Weapon || ResolveFixedSlot(category) != EquipmentSlot.None;

    /// <summary>
    /// Resolves the only slot an armor category may occupy. Weapons return
    /// <see cref="EquipmentSlot.None"/> because they target whichever quick slot is free.
    /// </summary>
    public static EquipmentSlot ResolveFixedSlot(LootCategory category) => category switch
    {
        LootCategory.Helmet => EquipmentSlot.Helmet,
        LootCategory.Armor => EquipmentSlot.Armor,
        LootCategory.Gloves => EquipmentSlot.Gloves,
        LootCategory.Boots => EquipmentSlot.Boots,
        _ => EquipmentSlot.None
    };

    public static bool IsCompatible(LootCategory category, EquipmentSlot slot) =>
        category == LootCategory.Weapon
            ? IsWeaponSlot(slot)
            : slot != EquipmentSlot.None && ResolveFixedSlot(category) == slot;

    public static EquipmentSlot FromWeaponSlot(WeaponSlot slot) => slot switch
    {
        WeaponSlot.Slot1 => EquipmentSlot.WeaponSlot1,
        WeaponSlot.Slot2 => EquipmentSlot.WeaponSlot2,
        _ => EquipmentSlot.None
    };

    public static WeaponSlot ToWeaponSlot(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.WeaponSlot1 => WeaponSlot.Slot1,
        EquipmentSlot.WeaponSlot2 => WeaponSlot.Slot2,
        _ => WeaponSlot.None
    };

    public static bool IsValidSlotValue(int value) =>
        value >= (int)EquipmentSlot.None && value <= (int)EquipmentSlot.Boots;
}
