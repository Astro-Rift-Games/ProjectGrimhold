using System;
using Fusion;

/// <summary>
/// One physical Inventory slot. Zero quantity/key is empty, not catalog entry zero.
/// Uses the existing 7-bit catalog and 5-bit Dungeon/participant encoding for Fusion.
/// </summary>
public struct LootInventorySlot : INetworkStruct, IEquatable<LootInventorySlot>
{
    private int _catalogIndexAndOriginSlot;
    private int _quantity;

    public int CatalogIndex => _catalogIndexAndOriginSlot >> 5;
    public int Quantity => _quantity;
    public bool IsEmpty => _quantity == 0 && _catalogIndexAndOriginSlot == 0;
    public bool IsValid => _quantity > 0 && RaidLootOriginPackedBuffer.TryUnpackKey(
        _catalogIndexAndOriginSlot, out _, out _);

    public RaidLootOrigin Origin
    {
        get
        {
            if (!IsValid)
                return default;

            int originSlot = _catalogIndexAndOriginSlot & 31;
            if (originSlot == 0)
                return RaidLootOrigin.Dungeon;

            RaidParticipantId.TryCreate(originSlot, out RaidParticipantId participantId);
            RaidLootOrigin.TryCreatePlayer(participantId, out RaidLootOrigin origin);
            return origin;
        }
    }

    public static bool TryCreate(int catalogIndex, int quantity, RaidLootOrigin origin, out LootInventorySlot slot)
    {
        slot = default;
        if (catalogIndex < 0 || catalogIndex >= RaidLootOriginPackedBuffer.MaximumCatalogEntries ||
            quantity <= 0 || !origin.IsValid)
        {
            return false;
        }

        slot._catalogIndexAndOriginSlot = RaidLootOriginPackedBuffer.PackKey(
            catalogIndex, origin.IsDungeon ? 0 : origin.PlayerParticipantId.Value);
        slot._quantity = quantity;
        return true;
    }

    /// <summary>Identity compatibility only; the rules separately enforce StackMax.</summary>
    public bool CanStackWith(in LootInventorySlot other) =>
        IsValid && other.IsValid && _catalogIndexAndOriginSlot == other._catalogIndexAndOriginSlot;

    public bool Equals(LootInventorySlot other) =>
        _catalogIndexAndOriginSlot == other._catalogIndexAndOriginSlot && _quantity == other._quantity;

    public override bool Equals(object obj) => obj is LootInventorySlot other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_catalogIndexAndOriginSlot, _quantity);
    public static bool operator ==(LootInventorySlot left, LootInventorySlot right) => left.Equals(right);
    public static bool operator !=(LootInventorySlot left, LootInventorySlot right) => !left.Equals(right);
}
