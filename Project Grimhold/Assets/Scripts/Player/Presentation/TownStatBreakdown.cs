using System;

/// <summary>One resource maximum split into its attribute and equipment contributions.</summary>
public readonly struct TownStatBreakdown : IEquatable<TownStatBreakdown>
{
    public int Total { get; }
    public int FromAttributes { get; }
    public int FromEquipment { get; }

    internal TownStatBreakdown(int fromAttributes, int fromEquipment)
    {
        FromAttributes = fromAttributes;
        FromEquipment = fromEquipment;
        Total = fromAttributes + fromEquipment;
    }

    public bool Equals(TownStatBreakdown other) =>
        FromAttributes == other.FromAttributes && FromEquipment == other.FromEquipment;

    public override bool Equals(object obj) => obj is TownStatBreakdown other && Equals(other);

    public override int GetHashCode() => (FromAttributes * 397) ^ FromEquipment;
}
