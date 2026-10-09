#if UNITY_EDITOR || DEVELOPMENT_BUILD
/// <summary>Layout used by the sandbox to place a batch of spawned entities.</summary>
public enum SandboxSpawnPattern : byte
{
    Ring = 0,
    Grid = 1,
    Line = 2
}
#endif
