using Fusion;

/// <summary>Isolated single-stack direct candidate; never attached to a production prefab.</summary>
public sealed class RaidLootOriginDirectPickupWeaverCandidate : NetworkBehaviour
{
    [Networked, Capacity(RaidLootOriginPackedBuffer.OriginsPerLoot)]
    private NetworkArray<RaidLootOriginDirectBucket> Buckets => default;

    [Networked]
    private int BucketCount { get; set; }
}
