/// <summary>
/// Authoritative outcome of a Raid Inventory/Equipment request.
/// Values are explicit because the result travels through the equipment RPC as an int.
/// </summary>
public enum EquipmentOperationResult
{
    None = 0,
    Succeeded = 1,
    InvalidRequest = 2,
    PlayerUnavailable = 3,
    InvalidEquipment = 4,
    // Value 5 is reserved by the former WeaponAlreadyEquipped result and must not be reused
    // with a different meaning while older clients can still send it.
    ItemNotOwned = 6,
    DependenciesUnavailable = 7,
    NoFreeWeaponSlot = 8,
    EmptySlot = 9,
    InventoryFull = 10,
    SlotOccupied = 11
}
