using Fusion;

/// <summary>
/// Pickup-specific direct origin record. Invalid participant is the explicit Dungeon encoding.
/// </summary>
public struct RaidLootOriginDirectBucket : INetworkStruct
{
    public RaidParticipantId ParticipantId;
    public int Amount;
}
