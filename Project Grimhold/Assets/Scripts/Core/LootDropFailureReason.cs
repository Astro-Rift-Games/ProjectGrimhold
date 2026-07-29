/// <summary>
/// Stable rejection reasons for materializing player inventory loot in the world.
/// </summary>
public enum LootDropFailureReason
{
    Uninitialized = 0,
    None = 1,
    PlayerUnavailable = 2,
    InvalidLoot = 3,
    InvalidAmount = 4,
    InsufficientAmount = 5,
    NoValidPosition = 6,
    SpawnFailed = 7,
    MissingAuthority = 8
}
