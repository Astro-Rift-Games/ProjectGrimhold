using Fusion;

/// <summary>Isolated full-capacity direct candidate; never attached to a production prefab.</summary>
public sealed class RaidLootOriginDirectEndpointWeaverCandidate : NetworkBehaviour
{
    [Networked, Capacity(RaidLootOriginPackedBuffer.MaximumBuckets)]
    private NetworkArray<RaidLootOriginDirectCatalogBucket> Buckets => default;

    [Networked]
    private int BucketCount { get; set; }
}
