/// <summary>
/// Authoritative compensation decisions for corpse conversion failures and forced defeats (FIX-42).
/// Prevents loot destruction when failures occur after player inventory has been cleared,
/// and directs liquidation and loss reporting during forced defeats.
/// </summary>
public enum PlayerCorpseCompensationAction
{
    /// <summary>
    /// Player inventory was not cleared; container contents are rolled back and container remains disabled.
    /// </summary>
    RollbackContainer,

    /// <summary>
    /// Player inventory was already cleared; container contents are preserved and container is enabled.
    /// </summary>
    PreserveAndEnableCorpse
}

public static class PlayerCorpseCompensationRule
{
    /// <summary>
    /// Determines whether to rollback the container or preserve and enable the corpse
    /// based on whether the player's inventory was already cleared before the error occurred.
    /// </summary>
    public static PlayerCorpseCompensationAction DetermineCompensationAction(bool playerCleared)
    {
        return playerCleared
            ? PlayerCorpseCompensationAction.PreserveAndEnableCorpse
            : PlayerCorpseCompensationAction.RollbackContainer;
    }

    /// <summary>
    /// Evaluates whether a forced defeat requires manual unowned item liquidation and loss summary publication
    /// because corpse conversion was not completed.
    /// </summary>
    public static bool RequiresForcedDefeatItemLiquidation(bool corpseGenerationCompleted)
    {
        return !corpseGenerationCompleted;
    }
}
