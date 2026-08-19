/// <summary>
/// Authoritative outcome of a Raid Inventory/Equipment request.
/// </summary>
public enum WeaponEquipResult
{
    None = 0,
    Succeeded = 1,
    InvalidRequest = 2,
    PlayerUnavailable = 3,
    InvalidWeapon = 4,
    WeaponAlreadyEquipped = 5,
    WeaponNotOwned = 6,
    DependenciesUnavailable = 7,
    NoFreeWeaponSlot = 8,
    EmptyWeaponSlot = 9,
    InventoryFull = 10
}
