using System;
using Fusion;

/// <summary>
/// One weapon unit held by a <see cref="NetworkLootContainer"/>: its template, Raid origin and packed
/// instance modifiers (ItemInstanceArchitecture section 6.3). A zero key is an empty unit address.
/// </summary>
public struct ContainerWeaponUnit : INetworkStruct, IEquatable<ContainerWeaponUnit>
{
    private const int OriginBits = 5;
    private const int OriginMask = (1 << OriginBits) - 1;

    private int _catalogIndexPlusOneAndOrigin;
    private WeaponInstanceModifiersNetworkData _modifiers;

    public bool IsEmpty => _catalogIndexPlusOneAndOrigin == 0;
    public int CatalogIndex => IsEmpty ? -1 : (_catalogIndexPlusOneAndOrigin >> OriginBits) - 1;
    public WeaponInstanceModifiersNetworkData Modifiers => _modifiers;

    public RaidLootOrigin Origin
    {
        get
        {
            if (IsEmpty) return default;
            int originSlot = _catalogIndexPlusOneAndOrigin & OriginMask;
            if (originSlot == 0) return RaidLootOrigin.Dungeon;
            RaidParticipantId.TryCreate(originSlot, out RaidParticipantId participantId);
            RaidLootOrigin.TryCreatePlayer(participantId, out RaidLootOrigin origin);
            return origin;
        }
    }

    public static bool TryCreate(int catalogIndex, RaidLootOrigin origin,
        WeaponInstanceModifiersNetworkData modifiers, out ContainerWeaponUnit unit)
    {
        unit = default;
        if (catalogIndex < 0 || catalogIndex >= RaidLootOriginPackedBuffer.MaximumCatalogEntries || !origin.IsValid)
            return false;
        int originSlot = origin.IsDungeon ? 0 : origin.PlayerParticipantId.Value;
        if (originSlot > OriginMask) return false;
        unit._catalogIndexPlusOneAndOrigin = ((catalogIndex + 1) << OriginBits) | originSlot;
        unit._modifiers = modifiers;
        return true;
    }

    public bool Equals(ContainerWeaponUnit other) =>
        _catalogIndexPlusOneAndOrigin == other._catalogIndexPlusOneAndOrigin && _modifiers.Equals(other._modifiers);

    public override bool Equals(object obj) => obj is ContainerWeaponUnit other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_catalogIndexPlusOneAndOrigin, _modifiers.PackedValue);
}
