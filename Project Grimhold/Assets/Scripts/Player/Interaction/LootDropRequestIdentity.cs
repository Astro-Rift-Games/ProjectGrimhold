using System;

/// <summary>
/// Primitive identity used to deduplicate one inventory drop request.
/// </summary>
public readonly struct LootDropRequestIdentity : IEquatable<LootDropRequestIdentity>
{
    public uint RequestSequence { get; }
    public int CatalogIndex { get; }
    public LootTransferQuantityMode QuantityMode { get; }
    public int SourceInventorySlotIndex { get; }
    public int ExpectedInventoryRevision { get; }
    public bool HasInventorySlotAddress => SourceInventorySlotIndex != -1 || ExpectedInventoryRevision != -1;

    public LootDropRequestIdentity(
        uint requestSequence,
        int catalogIndex,
        LootTransferQuantityMode quantityMode)
        : this(requestSequence, catalogIndex, quantityMode, -1, -1)
    {
    }

    public LootDropRequestIdentity(
        uint requestSequence,
        int catalogIndex,
        LootTransferQuantityMode quantityMode,
        int sourceInventorySlotIndex,
        int expectedInventoryRevision)
    {
        SourceInventorySlotIndex = sourceInventorySlotIndex;
        ExpectedInventoryRevision = expectedInventoryRevision;
        RequestSequence = requestSequence;
        CatalogIndex = catalogIndex;
        QuantityMode = quantityMode;
    }

    public bool Equals(LootDropRequestIdentity other) =>
        RequestSequence == other.RequestSequence &&
        CatalogIndex == other.CatalogIndex &&
        QuantityMode == other.QuantityMode &&
        SourceInventorySlotIndex == other.SourceInventorySlotIndex &&
        ExpectedInventoryRevision == other.ExpectedInventoryRevision;

    public override bool Equals(object obj) =>
        obj is LootDropRequestIdentity other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RequestSequence, CatalogIndex, (int)QuantityMode,
            SourceInventorySlotIndex, ExpectedInventoryRevision);

    public static bool operator ==(LootDropRequestIdentity left, LootDropRequestIdentity right) =>
        left.Equals(right);

    public static bool operator !=(LootDropRequestIdentity left, LootDropRequestIdentity right) =>
        !left.Equals(right);
}
