/// <summary>
/// Immutable local projection supplied to contextual action providers.
/// </summary>
public readonly struct LootContextActionContext
{
    public LootEntry Entry { get; }
    public LootDefinition Definition { get; }

    public bool IsValid => Entry.IsValid && Definition != null &&
        Definition.LootId == Entry.LootId;

    public LootContextActionContext(in LootEntry entry, LootDefinition definition)
    {
        Entry = entry;
        Definition = definition;
    }
}
