/// <summary>
/// Which origin buckets are drawn first when a quantity is resolved from a mixed-origin stack.
/// The resulting <see cref="RaidLootOriginTransfer"/> is always in canonical bucket order.
/// </summary>
public enum RaidLootOriginSelectionOrder
{
    /// <summary>Canonical order: Dungeon first, then Player origins by participant id.</summary>
    Canonical = 0,

    /// <summary>Player (carried from Town) origins by participant id first, then Dungeon.</summary>
    PlayerFirst = 1
}
