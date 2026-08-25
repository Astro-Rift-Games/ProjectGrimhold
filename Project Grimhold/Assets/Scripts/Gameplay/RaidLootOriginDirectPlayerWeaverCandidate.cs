using Fusion;

/// <summary>Direct Inventory plus six Equipment references; never attached to a production prefab.</summary>
public sealed class RaidLootOriginDirectPlayerWeaverCandidate : NetworkBehaviour
{
    [Networked, Capacity(RaidLootOriginPackedBuffer.MaximumBuckets)]
    private NetworkArray<RaidLootOriginDirectCatalogBucket> Buckets => default;

    [Networked]
    private int BucketCount { get; set; }

    [Networked] private RaidParticipantId EquipmentOrigin1 { get; set; }
    [Networked] private RaidParticipantId EquipmentOrigin2 { get; set; }
    [Networked] private RaidParticipantId EquipmentOrigin3 { get; set; }
    [Networked] private RaidParticipantId EquipmentOrigin4 { get; set; }
    [Networked] private RaidParticipantId EquipmentOrigin5 { get; set; }
    [Networked] private RaidParticipantId EquipmentOrigin6 { get; set; }
    [Networked] private int EquipmentOccupiedMask { get; set; }
    [Networked] private int EquipmentDungeonMask { get; set; }
}
