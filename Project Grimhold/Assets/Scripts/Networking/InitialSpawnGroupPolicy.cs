using UnityEngine;
using Spawning;

/// <summary>
/// Defines the explicit initial-spawn integration available for each scene group.
/// Unsupported groups never inherit another group's prefab implicitly.
/// </summary>
public static class InitialSpawnGroupPolicy
{
    public enum SpawnKind
    {
        Enemies,
        LootContainers,
        Breakables,
        Unsupported
    }

    public static SpawnKind Resolve(SpawnGroupType group)
    {
        return group switch
        {
            SpawnGroupType.Enemies => SpawnKind.Enemies,
            SpawnGroupType.Loot => SpawnKind.LootContainers,
            SpawnGroupType.Breakables => SpawnKind.Breakables,
            _ => SpawnKind.Unsupported
        };
    }

    /// <summary>
    /// Limits deterministic scene spawning to one object per configured point.
    /// </summary>
    public static int GetPointBoundedSpawnCount(SpawnGroupDefinition definition, out bool wasClamped)
    {
        int requested = Mathf.Max(0, definition?.Amount ?? 0);
        int availablePoints = definition?.SpawnPoints?.Length ?? 0;
        int spawnCount = Mathf.Min(requested, availablePoints);
        wasClamped = spawnCount < requested;
        return spawnCount;
    }

    public static int GetLootSpawnCount(SpawnGroupDefinition definition, out bool wasClamped)
    {
        return GetPointBoundedSpawnCount(definition, out wasClamped);
    }
}
