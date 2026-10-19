/// <summary>Which loot a <see cref="RaidResultsLootSummary"/> describes.</summary>
public enum RaidResultsLootKind
{
    None = 0,

    /// <summary>Loot obtained during the expedition that left the Dungeon with the player.</summary>
    Extracted = 1,

    /// <summary>Every carried unit that stayed in the Dungeon after Defeat or voluntary Abandon.</summary>
    Lost = 2
}
