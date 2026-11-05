using System;
using Fusion;

/// <summary>
/// One Fusion word per weapon unit. Scaling slots use attribute(3)+grade(3) bits each,
/// socket count uses 2 bits and gem catalog indices plus one use 7 bits each (28 bits total).
/// Zero is a plain, unscaled, socketless instance. Backend identity never enters this value.
/// </summary>
public struct WeaponInstanceModifiersNetworkData : INetworkStruct, IEquatable<WeaponInstanceModifiersNetworkData>
{
    private int _packedValue;
    private const int UsedBits = 28;
    public int PackedValue => _packedValue;
    public bool IsEmpty => _packedValue == 0;

    /// <summary>Untrusted wire data must pass TryResolve before being committed.</summary>
    public static WeaponInstanceModifiersNetworkData FromPacked(int value) => new() { _packedValue = value };

    public static bool TryCreate(in WeaponInstanceModifiers modifiers, CharacterAttribute naturalAttribute,
        LootDefinitionCatalog catalog, out WeaponInstanceModifiersNetworkData data, out string error)
    {
        data = default;
        if (!ValidateCatalog(catalog, out error) || !modifiers.TryValidate(naturalAttribute, out error))
            return false;
        if (!TryEncodeGem(modifiers, 0, catalog, out int first, out error) ||
            !TryEncodeGem(modifiers, 1, catalog, out int second, out error))
            return false;
        data._packedValue = EncodeScaling(modifiers.Primary) | (EncodeScaling(modifiers.Secondary) << 6) |
            (modifiers.GemSocketCount << 12) | (first << 14) | (second << 21);
        return true;
    }

    public readonly bool TryResolve(LootDefinitionCatalog catalog, CharacterAttribute naturalAttribute,
        out WeaponInstanceModifiers modifiers, out string error)
    {
        modifiers = default;
        if (!ValidateCatalog(catalog, out error)) return false;
        if (((uint)_packedValue >> UsedBits) != 0)
        {
            error = "Weapon modifier word contains reserved bits.";
            return false;
        }
        if (!TryDecodeScaling(_packedValue & 63, out var primary, out error) ||
            !TryDecodeScaling((_packedValue >> 6) & 63, out var secondary, out error) ||
            !TryDecodeGem((_packedValue >> 14) & 127, catalog, out var first, out error) ||
            !TryDecodeGem((_packedValue >> 21) & 127, catalog, out var second, out error))
            return false;
        return WeaponInstanceModifiers.TryCreate(primary, secondary, naturalAttribute,
            (_packedValue >> 12) & 3, first, second, out modifiers, out error);
    }

    public bool Equals(WeaponInstanceModifiersNetworkData other) => _packedValue == other._packedValue;
    public override bool Equals(object obj) => obj is WeaponInstanceModifiersNetworkData other && Equals(other);
    public override int GetHashCode() => _packedValue;

    private static bool ValidateCatalog(LootDefinitionCatalog catalog, out string error)
    {
        error = null;
        if (catalog != null && catalog.DefinitionCount < RaidLootOriginPackedBuffer.MaximumCatalogEntries)
            return true;
        error = "Weapon modifier transport requires a loot catalog with fewer than 128 entries.";
        return false;
    }

    private static int EncodeScaling(WeaponScalingModifier value) =>
        value.IsPresent ? (int)value.Attribute | ((int)value.Grade << 3) : 0;

    private static bool TryDecodeScaling(int bits, out WeaponScalingModifier modifier, out string error)
    {
        modifier = default;
        error = null;
        if (bits == 0) return true;
        return WeaponScalingModifier.TryCreate((CharacterAttribute)(bits & 7),
            (WeaponScalingGrade)(bits >> 3), out modifier, out error);
    }

    private static bool TryEncodeGem(in WeaponInstanceModifiers modifiers, int socketIndex,
        LootDefinitionCatalog catalog, out int indexPlusOne, out string error)
    {
        indexPlusOne = 0;
        error = null;
        if (!modifiers.TryGetGemSocket(socketIndex, out var socket) || !socket.IsOccupied) return true;
        if (!catalog.TryGetIndex(socket.GemLootId, out int index) || index < 0 || index >= 127 ||
            !catalog.TryGetByIndex(index, out LootDefinition gem) || gem.GemDefinition == null)
        {
            error = $"Installed gem '{socket.GemLootId}' is missing or outside the packed catalog range.";
            return false;
        }
        indexPlusOne = index + 1;
        return true;
    }

    private static bool TryDecodeGem(int indexPlusOne, LootDefinitionCatalog catalog,
        out WeaponGemSocket socket, out string error)
    {
        socket = default;
        error = null;
        if (indexPlusOne == 0) return true;
        if (!catalog.TryGetByIndex(indexPlusOne - 1, out LootDefinition gem) || gem == null || gem.GemDefinition == null)
        {
            error = $"Installed gem catalog index '{indexPlusOne - 1}' is not present in the loot catalog.";
            return false;
        }
        return WeaponGemSocket.TryCreateOccupied(gem.LootId, out socket, out error);
    }
}
