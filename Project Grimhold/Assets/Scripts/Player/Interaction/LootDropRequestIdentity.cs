using System;

/// <summary>
/// Primitive identity used to deduplicate one inventory drop request.
/// </summary>
public readonly struct LootDropRequestIdentity : IEquatable<LootDropRequestIdentity>
{
    public uint RequestSequence { get; }
    public int CatalogIndex { get; }
    public LootTransferQuantityMode QuantityMode { get; }

    public LootDropRequestIdentity(
        uint requestSequence,
        int catalogIndex,
        LootTransferQuantityMode quantityMode)
    {
        RequestSequence = requestSequence;
        CatalogIndex = catalogIndex;
        QuantityMode = quantityMode;
    }

    public bool Equals(LootDropRequestIdentity other) =>
        RequestSequence == other.RequestSequence &&
        CatalogIndex == other.CatalogIndex &&
        QuantityMode == other.QuantityMode;

    public override bool Equals(object obj) =>
        obj is LootDropRequestIdentity other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RequestSequence, CatalogIndex, (int)QuantityMode);

    public static bool operator ==(LootDropRequestIdentity left, LootDropRequestIdentity right) =>
        left.Equals(right);

    public static bool operator !=(LootDropRequestIdentity left, LootDropRequestIdentity right) =>
        !left.Equals(right);
}
