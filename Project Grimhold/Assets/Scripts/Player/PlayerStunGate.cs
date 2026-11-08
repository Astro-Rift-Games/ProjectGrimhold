/// <summary>Shared Stun predicate for action choke points that only hold an <see cref="ICharacter"/>.</summary>
public static class PlayerStunGate
{
    /// <summary>True when the character is a player under an active Stun.</summary>
    public static bool IsStunned(ICharacter character) => character is PlayerCharacter player && player.IsStunned;
}
