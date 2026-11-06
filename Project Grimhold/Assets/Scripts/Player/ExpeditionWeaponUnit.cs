using System;

/// <summary>
/// One weapon unit that carries instance modifiers inside an expedition snapshot. Plain weapon
/// units stay implicit in the Loot and provenance totals (ItemInstanceArchitecture section 6.3).
/// </summary>
public readonly struct ExpeditionWeaponUnit : IEquatable<ExpeditionWeaponUnit>
{
    public ExpeditionWeaponUnit(LootId lootId, RaidLootOrigin origin, WeaponInstanceModifiersNetworkData modifiers)
    {
        LootId = lootId;
        Origin = origin;
        Modifiers = modifiers;
    }

    public LootId LootId { get; }
    public RaidLootOrigin Origin { get; }
    public WeaponInstanceModifiersNetworkData Modifiers { get; }

    public bool Equals(ExpeditionWeaponUnit other) =>
        LootId == other.LootId && Origin == other.Origin && Modifiers.Equals(other.Modifiers);

    public override bool Equals(object obj) => obj is ExpeditionWeaponUnit other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(LootId, Origin, Modifiers.PackedValue);

    /// <summary>Order-insensitive multiset comparison; snapshots hold at most 34 units.</summary>
    public static bool SequenceEquivalent(
        System.Collections.Generic.IReadOnlyList<ExpeditionWeaponUnit> left,
        System.Collections.Generic.IReadOnlyList<ExpeditionWeaponUnit> right)
    {
        int leftCount = left?.Count ?? 0;
        if (leftCount != (right?.Count ?? 0)) return false;
        if (leftCount == 0) return true;
        var remaining = new System.Collections.Generic.List<ExpeditionWeaponUnit>(right);
        for (int index = 0; index < leftCount; index++)
        {
            int match = remaining.IndexOf(left[index]);
            if (match < 0) return false;
            remaining.RemoveAt(match);
        }
        return true;
    }
}
