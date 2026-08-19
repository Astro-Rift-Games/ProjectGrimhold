/// <summary>
/// Authoritative outcome of a request to equip one weapon from Raid inventory.
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
    DependenciesUnavailable = 7
}
