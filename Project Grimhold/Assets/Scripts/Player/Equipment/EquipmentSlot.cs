/// <summary>
/// Identifies one Equipment slot of the player. The two weapon values intentionally match
/// <see cref="WeaponSlot"/> so the value already replicated by the active-weapon selection and
/// transported by the equipment RPC keeps its meaning.
/// </summary>
public enum EquipmentSlot : byte
{
    None = 0,
    WeaponSlot1 = 1,
    WeaponSlot2 = 2,
    Helmet = 3,
    Armor = 4,
    Gloves = 5,
    Boots = 6
}
