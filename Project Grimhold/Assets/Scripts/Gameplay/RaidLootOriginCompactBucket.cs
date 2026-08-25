using Fusion;

/// <summary>Compact logical record encoded into the replicated packed buffer.</summary>
public struct RaidLootOriginCompactBucket : INetworkStruct
{
    public int CatalogIndexAndOriginSlot;
    public int Amount;
}
