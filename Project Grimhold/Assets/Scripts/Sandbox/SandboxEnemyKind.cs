#if UNITY_EDITOR || DEVELOPMENT_BUILD
/// <summary>Kind of entity the sandbox spawner can create.</summary>
public enum SandboxEnemyKind : byte
{
    Melee = 0,
    Ranged = 1,
    Dummy = 2
}
#endif
