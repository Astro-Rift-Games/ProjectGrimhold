/// <summary>
/// Shared Downed predicate for action choke points that only hold an <see cref="ICharacter"/>.
/// Extraction is intentionally not gated by it.
/// </summary>
public static class PlayerDownedGate
{
    /// <summary>True when the character is a player currently in the Downed state.</summary>
    public static bool IsDowned(ICharacter character)
    {
        if (character is PlayerCharacter player)
        {
            return player.IsDowned;
        }

        if (character != null)
        {
            var prop = character.GetType().GetProperty("IsDowned");
            if (prop != null && prop.PropertyType == typeof(bool))
            {
                return (bool)prop.GetValue(character);
            }
        }

        return false;
    }
}
