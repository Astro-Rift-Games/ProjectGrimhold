/// <summary>
/// Shared Downed predicate for action choke points that only hold an <see cref="ICharacter"/>.
/// Extraction is intentionally not gated by it.
/// </summary>
public static class PlayerDownedGate
{
    /// <summary>True when the character is a player currently in the Downed state.</summary>
    public static bool IsDowned(ICharacter character) =>
        character is PlayerCharacter player && player.IsDowned;
}
