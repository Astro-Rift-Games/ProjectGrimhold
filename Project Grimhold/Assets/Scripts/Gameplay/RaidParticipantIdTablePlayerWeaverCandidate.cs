using Fusion;

/// <summary>Isolated local RaidParticipantId table, packed Inventory and Equipment indices.</summary>
public sealed class RaidParticipantIdTablePlayerWeaverCandidate : NetworkBehaviour
{
    [Networked, Capacity(RaidSessionRules.MaxParticipants)]
    private NetworkArray<RaidParticipantId> Participants => default;

    [Networked]
    private RaidLootOriginPackedState InventoryBuckets { get; set; }

    [Networked]
    private int EquipmentOriginIndices { get; set; }
}
