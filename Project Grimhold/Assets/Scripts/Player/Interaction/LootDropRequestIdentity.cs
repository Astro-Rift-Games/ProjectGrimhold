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
    /// <summary>Chosen partial amount for <see cref="LootTransferQuantityMode.ExactAmount"/>; zero otherwise.</summary>
    public int RequestedAmount { get; }
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
        : this(requestSequence, catalogIndex, quantityMode, sourceInventorySlotIndex, expectedInventoryRevision, 0)
    {
    }

    public LootDropRequestIdentity(
        uint requestSequence,
        int catalogIndex,
        LootTransferQuantityMode quantityMode,
        int sourceInventorySlotIndex,
        int expectedInventoryRevision,
        int requestedAmount)
    {
        RequestedAmount = requestedAmount;
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
        ExpectedInventoryRevision == other.ExpectedInventoryRevision &&
        RequestedAmount == other.RequestedAmount;

    public override bool Equals(object obj) =>
        obj is LootDropRequestIdentity other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RequestSequence, CatalogIndex, (int)QuantityMode,
            SourceInventorySlotIndex, ExpectedInventoryRevision, RequestedAmount);

    public static bool operator ==(LootDropRequestIdentity left, LootDropRequestIdentity right) =>
        left.Equals(right);

    public static bool operator !=(LootDropRequestIdentity left, LootDropRequestIdentity right) =>
        !left.Equals(right);
}
