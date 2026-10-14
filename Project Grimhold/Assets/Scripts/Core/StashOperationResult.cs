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
    /// The transaction has already been applied, preventing duplicates.
    /// </summary>
    AlreadyApplied,

    /// <summary>
    /// The provided inventory snapshot was invalid or empty.
    /// </summary>
    InvalidInventory,

    /// <summary>
    /// The underlying persistence mechanism failed to save the loot.
    /// </summary>
    PersistenceFailed,

    /// <summary>The confirmed character attributes do not satisfy a weapon assignment.</summary>
    AttributeRequirementsNotMet,

    /// <summary>The hydration process failed during initialization or reconciliation.</summary>
    HydrationFailed,

    /// <summary>
    /// A raid reservation currently owns the Inventory and Equipment, so Stash, Loadout and
    /// Equipment cannot change until it is confirmed or rolled back.
    /// </summary>
    ReservationPending
}
