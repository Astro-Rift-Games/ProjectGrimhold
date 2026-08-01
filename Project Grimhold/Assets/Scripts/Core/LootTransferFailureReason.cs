/// <summary>
/// Stable domain reasons for rejecting a loot movement.
/// </summary>
public enum LootTransferFailureReason
{
    Uninitialized,
    None,
    InvalidLoot,
    InvalidAmount,
    SourceNotFound,
    DestinationNotFound,
    InsufficientAmount,
    InventoryFull,
    OutOfRange,
    MissingAuthority,
    ContainerUnavailable,
    Overflow,
    PlayerUnavailable
}
