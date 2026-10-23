/// <summary>Shared action gate reading the player's authoritative consumable execution.</summary>
public static class PlayerConsumptionGate
{
    public static bool IsConsuming(ICharacter character) => character is PlayerCharacter player && player.IsConsuming;
}
