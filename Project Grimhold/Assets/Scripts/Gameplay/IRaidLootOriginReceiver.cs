/// <summary>Authoritative Raid provenance capability paired with an ILootReceiver.</summary>
public interface IRaidLootOriginReceiver
{
    bool IsRaidLootOriginAware { get; }

    LootTransferFailureReason ValidateRaidLootOriginReceive(
        in LootTransferRequest request,
        RaidLootOriginTransfer transfer);

    void CommitRaidLootReceive(
        in LootTransferRequest request,
        RaidLootOriginTransfer transfer);
}
