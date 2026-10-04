/// <summary>
/// Shared reviver-side hook for action choke points that only hold an <see cref="ICharacter"/>.
/// An action that is incompatible with holding a revive interrupts the session first; the action
/// itself then proceeds under its normal rules because the recovery has already ended.
/// </summary>
public static class PlayerReviveGate
{
    /// <summary>
    /// State Authority only. No-op unless the character is a player currently reviving another avatar.
    /// </summary>
    public static void InterruptIfReviving(ICharacter character)
    {
        if (character is PlayerCharacter player &&
            player.TryGetComponent(out PlayerDownedRecoveryNetworkController recovery))
        {
            recovery.InterruptRevivedTarget(DownedRecoveryInterruptReason.IncompatibleReviverAction);
        }
    }
}
