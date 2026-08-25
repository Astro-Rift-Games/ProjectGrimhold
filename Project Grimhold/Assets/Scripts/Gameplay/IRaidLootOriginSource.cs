/// <summary>Authoritative Raid provenance capability paired with an ILootExtractor.</summary>
public interface IRaidLootOriginSource
{
    bool IsRaidLootOriginAware { get; }

    bool TryResolveRaidLootOriginTransfer(
        in LootTransferRequest request,
        out RaidLootOriginTransfer transfer);

    void CommitRaidLootExtraction(
        in LootTransferRequest request,
        RaidLootOriginTransfer transfer);
}
