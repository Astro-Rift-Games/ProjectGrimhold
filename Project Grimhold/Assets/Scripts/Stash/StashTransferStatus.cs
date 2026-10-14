/// <summary>How a Stash action coordinated by <see cref="StashTransferCoordinator"/> ended.</summary>
public enum StashTransferStatus
{
    /// <summary>The action completed locally and, when it has a remote part, remotely.</summary>
    Success,

    /// <summary>
    /// The local rules rejected the action before any remote call; <see cref="StashTransferOutcome.Rule"/>
    /// says why and nothing changed.
    /// </summary>
    Rejected,

    /// <summary>The Town Equipment gate is closed (the player is Ready), so nothing changed.</summary>
    Blocked,

    /// <summary>
    /// The backend rejected or could not confirm the action and the profile was reconciled with it.
    /// The mutation may have committed remotely; reconciliation can change local ownership.
    /// </summary>
    RemoteFailed,

    /// <summary>
    /// The backend accepted a transfer but the local profile could not apply it, so the profile
    /// was reconciled with the backend.
    /// </summary>
    Diverged,

    /// <summary>
    /// A bulk sequence has unconfirmed or not-attempted stacks. Completed counts confirmed
    /// successful attempts; Failed counts all other original source IDs. Neither infers actual moves.
    /// </summary>
    Partial,

    /// <summary>
    /// No remote inventory service is available. A transfer changed nothing; an equip change was
    /// applied locally but is not persisted.
    /// </summary>
    Unavailable,

    /// <summary>
    /// Reconciliation failed after a remote error or local divergence. Bulk processing stops;
    /// the original remote error or local rule and the recovery failure remain distinguishable.
    /// </summary>
    RecoveryFailed
}
