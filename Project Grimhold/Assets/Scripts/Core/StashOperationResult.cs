/// <summary>
/// Represents the outcome of a stash operation.
/// </summary>
public enum StashOperationResult
{
    /// <summary>
    /// The operation completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The stash already contains secured loot for this transaction, preventing duplicates.
    /// </summary>
    AlreadySecured,

    /// <summary>
    /// The provided inventory snapshot was invalid or empty.
    /// </summary>
    InvalidInventory,

    /// <summary>
    /// The underlying persistence mechanism failed to save the loot.
    /// </summary>
    PersistenceFailed
}
