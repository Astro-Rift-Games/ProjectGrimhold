/// <summary>Authoritative one-shot lifecycle for a table-backed Loot source.</summary>
public enum LootSourceGenerationState : byte
{
    NotApplicable = 0,
    Pending = 1,
    Resolved = 2,
    Failed = 3
}
