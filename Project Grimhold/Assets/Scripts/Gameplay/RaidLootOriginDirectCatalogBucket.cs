using Fusion;

/// <summary>Direct multi-stack candidate record used only for reproducible Weaver measurement.</summary>
public struct RaidLootOriginDirectCatalogBucket : INetworkStruct
{
    public RaidParticipantId ParticipantId;
    public int CatalogIndex;
    public int Amount;
}
