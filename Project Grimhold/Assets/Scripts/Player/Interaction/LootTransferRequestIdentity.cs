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
    public LootTransferQuantityMode QuantityMode { get; }
    public int SourceInventorySlotIndex { get; }
    public int ExpectedInventoryRevision { get; }
    public bool HasInventorySlotAddress => SourceInventorySlotIndex != -1 || ExpectedInventoryRevision != -1;

    public LootTransferRequestIdentity(
        uint requestSequence,
        EntityId sourceId,
        EntityId destinationId,
        int catalogIndex,
        LootTransferQuantityMode quantityMode)
        : this(requestSequence, sourceId, destinationId, catalogIndex, quantityMode, -1, -1)
    {
    }

    public LootTransferRequestIdentity(
        uint requestSequence,
        EntityId sourceId,
        EntityId destinationId,
        int catalogIndex,
        LootTransferQuantityMode quantityMode,
        int sourceInventorySlotIndex,
        int expectedInventoryRevision)
    {
        SourceInventorySlotIndex = sourceInventorySlotIndex;
        ExpectedInventoryRevision = expectedInventoryRevision;
        RequestSequence = requestSequence;
        SourceId = sourceId;
        DestinationId = destinationId;
        CatalogIndex = catalogIndex;
        QuantityMode = quantityMode;
    }

    public bool Equals(LootTransferRequestIdentity other) =>
        RequestSequence == other.RequestSequence &&
        SourceId == other.SourceId &&
        DestinationId == other.DestinationId &&
        CatalogIndex == other.CatalogIndex &&
        QuantityMode == other.QuantityMode &&
        SourceInventorySlotIndex == other.SourceInventorySlotIndex &&
        ExpectedInventoryRevision == other.ExpectedInventoryRevision;

    public override bool Equals(object obj) => obj is LootTransferRequestIdentity other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = (int)RequestSequence;
            hashCode = (hashCode * 397) ^ SourceId.GetHashCode();
            hashCode = (hashCode * 397) ^ DestinationId.GetHashCode();
            hashCode = (hashCode * 397) ^ CatalogIndex;
            hashCode = (hashCode * 397) ^ (int)QuantityMode;
            hashCode = (hashCode * 397) ^ SourceInventorySlotIndex;
            return (hashCode * 397) ^ ExpectedInventoryRevision;
        }
    }

    public static bool operator ==(LootTransferRequestIdentity left, LootTransferRequestIdentity right) => left.Equals(right);
    public static bool operator !=(LootTransferRequestIdentity left, LootTransferRequestIdentity right) => !left.Equals(right);
}
