/// <summary>
/// Identifies one Equipment slot. Historical numeric values are retained for the two former
/// weapon slots and now represent the Main Hand of Set A and Set B.
/// </summary>
public enum EquipmentSlot : byte
{
    None = 0,
    WeaponSetAMainHand = 1,
    WeaponSetBMainHand = 2,
    Helmet = 3,
    Armor = 4,
    Gloves = 5,
    Boots = 6,
    WeaponSetAOffHand = 7,
    WeaponSetBOffHand = 8
}
