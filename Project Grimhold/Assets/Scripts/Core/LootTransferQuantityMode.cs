/// <summary>
/// Describes how State Authority resolves the amount for one loot transfer intention.
/// </summary>
public enum LootTransferQuantityMode
{
    SingleUnit = 1,
    FullStack = 2,

    /// <summary>
    /// Addressed inventory drop of a chosen partial amount in [1, slot quantity - 1]. Only the
    /// exact-amount resolver accepts it; every other transfer path rejects it as unsupported.
    /// </summary>
    ExactAmount = 3
}
