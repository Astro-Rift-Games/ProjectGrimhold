/// <summary>
/// Pure rule for the state a failed corpse conversion leaves behind (FIX-53 S7).
/// </summary>
public static class PlayerCorpseGenerationRetryRule
{
    /// <summary>
    /// A failed attempt may be retried only when it left nothing behind: the player's inventory and
    /// equipment still equal the captured snapshot, and the co-located container is again unavailable
    /// and empty. Anything else stays terminal so a retry can never lose or duplicate items.
    /// </summary>
    public static bool CanRetryAfterFailure(bool playerInventoryIntact, bool corpseContainerClean)
    {
        return playerInventoryIntact && corpseContainerClean;
    }
}
