/// <summary>
/// Immutable local projection supplied to contextual action providers.
/// </summary>
public readonly struct LootContextActionContext
{
    public LootEntry Entry { get; }
    public LootDefinition Definition { get; }
    // default(struct) must expose the legacy -1/-1 pair, not physical address/revision zero.
    private readonly int? _sourceInventorySlotIndex;
    private readonly int? _expectedInventoryRevision;
    public int SourceInventorySlotIndex => _sourceInventorySlotIndex ?? -1;
    public int ExpectedInventoryRevision => _expectedInventoryRevision ?? -1;
    public bool HasInventorySlotAddress => SourceInventorySlotIndex != -1 || ExpectedInventoryRevision != -1;

    public bool IsValid => Entry.IsValid && Definition != null &&
        Definition.LootId == Entry.LootId &&
        LootTransferRequest.IsValidInventorySource(SourceInventorySlotIndex, ExpectedInventoryRevision);

    public LootContextActionContext(in LootEntry entry, LootDefinition definition)
        : this(entry, definition, -1, -1) { }

    public LootContextActionContext(in LootEntry entry, LootDefinition definition,
        int sourceInventorySlotIndex, int expectedInventoryRevision)
    {
        Entry = entry;
        Definition = definition;
        _sourceInventorySlotIndex = sourceInventorySlotIndex;
        _expectedInventoryRevision = expectedInventoryRevision;
    }
}
