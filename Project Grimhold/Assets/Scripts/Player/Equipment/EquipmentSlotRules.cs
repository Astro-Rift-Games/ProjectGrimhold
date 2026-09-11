/// <summary>Single source of truth for Equipment slot and Weapon Set compatibility.</summary>
public static class EquipmentSlotRules
{
    public const int SlotCount = 8;

    public static readonly EquipmentSlot[] AllSlots =
    {
        EquipmentSlot.WeaponSetAMainHand, EquipmentSlot.WeaponSetBMainHand,
        EquipmentSlot.Helmet, EquipmentSlot.Armor, EquipmentSlot.Gloves, EquipmentSlot.Boots,
        EquipmentSlot.WeaponSetAOffHand, EquipmentSlot.WeaponSetBOffHand
    };

    public static readonly EquipmentSlot[] HandSlots =
    {
        EquipmentSlot.WeaponSetAMainHand, EquipmentSlot.WeaponSetAOffHand,
        EquipmentSlot.WeaponSetBMainHand, EquipmentSlot.WeaponSetBOffHand
    };

    public static bool IsHandSlot(EquipmentSlot slot) =>
        slot == EquipmentSlot.WeaponSetAMainHand || slot == EquipmentSlot.WeaponSetAOffHand ||
        slot == EquipmentSlot.WeaponSetBMainHand || slot == EquipmentSlot.WeaponSetBOffHand;

    public static bool IsMainHandSlot(EquipmentSlot slot) =>
        slot == EquipmentSlot.WeaponSetAMainHand || slot == EquipmentSlot.WeaponSetBMainHand;

    public static bool IsOffHandSlot(EquipmentSlot slot) =>
        slot == EquipmentSlot.WeaponSetAOffHand || slot == EquipmentSlot.WeaponSetBOffHand;

    public static bool IsArmorSlot(EquipmentSlot slot) =>
        slot == EquipmentSlot.Helmet || slot == EquipmentSlot.Armor ||
        slot == EquipmentSlot.Gloves || slot == EquipmentSlot.Boots;

    public static bool IsEquipmentSlot(EquipmentSlot slot) => IsHandSlot(slot) || IsArmorSlot(slot);

    public static bool IsEquippableCategory(LootCategory category) =>
        category == LootCategory.Weapon || category == LootCategory.Shield ||
        ResolveFixedSlot(category) != EquipmentSlot.None;

    public static EquipmentSlot ResolveFixedSlot(LootCategory category) => category switch
    {
        LootCategory.Helmet => EquipmentSlot.Helmet,
        LootCategory.Armor => EquipmentSlot.Armor,
        LootCategory.Gloves => EquipmentSlot.Gloves,
        LootCategory.Boots => EquipmentSlot.Boots,
        _ => EquipmentSlot.None
    };

    public static bool IsCompatible(LootCategory category, EquipmentSlot slot) =>
        category switch
        {
            LootCategory.Weapon => IsHandSlot(slot),
            LootCategory.Shield => IsOffHandSlot(slot),
            _ => slot != EquipmentSlot.None && ResolveFixedSlot(category) == slot
        };

    public static bool IsCompatible(LootDefinition definition, EquipmentSlot slot)
    {
        if (definition == null || !IsCompatible(definition.Category, slot))
        {
            return false;
        }

        return definition.Category switch
        {
            LootCategory.Weapon => IsCompatible(definition.WeaponDefinition, slot),
            LootCategory.Shield => definition.ShieldDefinition != null &&
                definition.ShieldDefinition.TryValidate(out _),
            _ => true
        };
    }

    public static bool IsCompatible(WeaponDefinition weapon, EquipmentSlot slot) =>
        weapon != null && IsHandSlot(slot) &&
        (weapon.Handedness == WeaponHandedness.OneHanded || IsMainHandSlot(slot));

    public static WeaponSetSlot GetWeaponSet(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.WeaponSetAMainHand or EquipmentSlot.WeaponSetAOffHand => WeaponSetSlot.SetA,
        EquipmentSlot.WeaponSetBMainHand or EquipmentSlot.WeaponSetBOffHand => WeaponSetSlot.SetB,
        _ => WeaponSetSlot.None
    };

    public static EquipmentSlot GetMainHandSlot(WeaponSetSlot set) => set switch
    {
        WeaponSetSlot.SetA => EquipmentSlot.WeaponSetAMainHand,
        WeaponSetSlot.SetB => EquipmentSlot.WeaponSetBMainHand,
        _ => EquipmentSlot.None
    };

    public static EquipmentSlot GetOffHandSlot(WeaponSetSlot set) => set switch
    {
        WeaponSetSlot.SetA => EquipmentSlot.WeaponSetAOffHand,
        WeaponSetSlot.SetB => EquipmentSlot.WeaponSetBOffHand,
        _ => EquipmentSlot.None
    };

    public static EquipmentSlot GetMainHandSlot(EquipmentSlot handSlot) =>
        GetMainHandSlot(GetWeaponSet(handSlot));

    public static EquipmentSlot GetOffHandSlot(EquipmentSlot handSlot) =>
        GetOffHandSlot(GetWeaponSet(handSlot));

    public static bool IsValidSlotValue(int value) =>
        value >= (int)EquipmentSlot.None && value <= (int)EquipmentSlot.WeaponSetBOffHand;
}
