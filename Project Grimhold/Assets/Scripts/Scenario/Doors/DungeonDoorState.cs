/// <summary>
/// Replicated lifecycle of a Dungeon door. Only <see cref="Closed"/> and <see cref="Open"/> accept interaction.
/// </summary>
public enum DungeonDoorState : byte
{
    Closed,
    Opening,
    Open,
    Closing
}
