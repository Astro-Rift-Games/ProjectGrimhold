using System;

/// <summary>
/// Compact identity used by the local transport queue and bounded idempotency cache.
/// </summary>
public readonly struct LootTransferRequestIdentity : IEquatable<LootTransferRequestIdentity>
{
    public uint RequestSequence { get; }
    public EntityId SourceId { get; }
    public EntityId DestinationId { get; }
    public int CatalogIndex { get; }

    public LootTransferRequestIdentity(
        uint requestSequence,
        EntityId sourceId,
        EntityId destinationId,
        int catalogIndex)
    {
        RequestSequence = requestSequence;
        SourceId = sourceId;
        DestinationId = destinationId;
        CatalogIndex = catalogIndex;
    }

    public bool Equals(LootTransferRequestIdentity other) =>
        RequestSequence == other.RequestSequence &&
        SourceId == other.SourceId &&
        DestinationId == other.DestinationId &&
        CatalogIndex == other.CatalogIndex;

    public override bool Equals(object obj) => obj is LootTransferRequestIdentity other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = (int)RequestSequence;
            hashCode = (hashCode * 397) ^ SourceId.GetHashCode();
            hashCode = (hashCode * 397) ^ DestinationId.GetHashCode();
            return (hashCode * 397) ^ CatalogIndex;
        }
    }

    public static bool operator ==(LootTransferRequestIdentity left, LootTransferRequestIdentity right) => left.Equals(right);
    public static bool operator !=(LootTransferRequestIdentity left, LootTransferRequestIdentity right) => !left.Equals(right);
}
