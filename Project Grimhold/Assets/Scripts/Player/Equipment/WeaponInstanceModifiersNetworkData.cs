using Fusion;

/// <summary>
/// Blittable network form of <see cref="WeaponInstanceModifiers"/>. Installed gems use the
/// catalog's deterministic network index, offset by one so the default value is an empty socket.
/// It is not replicated by any runtime object until unique Item Instances exist end to end.
/// </summary>
public struct WeaponInstanceModifiersNetworkData : INetworkStruct
{
    private byte _primaryAttribute;
    private byte _primaryGrade;
    private byte _secondaryAttribute;
    private byte _secondaryGrade;
    private byte _gemSocketCount;
    private int _firstGemCatalogIndexPlusOne;
    private int _secondGemCatalogIndexPlusOne;

    public static bool TryCreate(
        in WeaponInstanceModifiers modifiers,
        CharacterAttribute naturalAttribute,
        LootDefinitionCatalog catalog,
        out WeaponInstanceModifiersNetworkData data,
        out string error)
    {
        data = default;
        if (catalog == null)
        {
            error = "A loot catalog is required to encode weapon instance gems.";
            return false;
        }

        if (!modifiers.TryValidate(naturalAttribute, out error))
        {
            return false;
        }

        WeaponScalingModifier primary = modifiers.Primary;
        WeaponScalingModifier secondary = modifiers.Secondary;
        if (!TryEncodeGem(modifiers, 0, catalog, out int firstGem, out error) ||
            !TryEncodeGem(modifiers, 1, catalog, out int secondGem, out error))
        {
            return false;
        }

        data = new WeaponInstanceModifiersNetworkData
        {
            _primaryAttribute = (byte)primary.Attribute,
            _primaryGrade = (byte)primary.Grade,
            _secondaryAttribute = (byte)secondary.Attribute,
            _secondaryGrade = (byte)secondary.Grade,
            _gemSocketCount = (byte)modifiers.GemSocketCount,
            _firstGemCatalogIndexPlusOne = firstGem,
            _secondGemCatalogIndexPlusOne = secondGem
        };
        return true;
    }

    public readonly bool TryResolve(
        LootDefinitionCatalog catalog,
        CharacterAttribute naturalAttribute,
        out WeaponInstanceModifiers modifiers,
        out string error)
    {
        modifiers = default;
        if (catalog == null)
        {
            error = "A loot catalog is required to decode weapon instance gems.";
            return false;
        }

        if (!TryDecodeModifier(_primaryAttribute, _primaryGrade, out WeaponScalingModifier primary, out error) ||
            !TryDecodeModifier(_secondaryAttribute, _secondaryGrade, out WeaponScalingModifier secondary, out error) ||
            !TryDecodeGem(_firstGemCatalogIndexPlusOne, catalog, out WeaponGemSocket firstGem, out error) ||
            !TryDecodeGem(_secondGemCatalogIndexPlusOne, catalog, out WeaponGemSocket secondGem, out error))
        {
            return false;
        }

        return WeaponInstanceModifiers.TryCreate(
            primary,
            secondary,
            naturalAttribute,
            _gemSocketCount,
            firstGem,
            secondGem,
            out modifiers,
            out error);
    }

    private static bool TryEncodeGem(
        in WeaponInstanceModifiers modifiers,
        int socketIndex,
        LootDefinitionCatalog catalog,
        out int catalogIndexPlusOne,
        out string error)
    {
        catalogIndexPlusOne = 0;
        error = null;
        if (!modifiers.TryGetGemSocket(socketIndex, out WeaponGemSocket socket) || !socket.IsOccupied)
        {
            return true;
        }

        if (!catalog.TryGetIndex(socket.GemLootId, out int catalogIndex))
        {
            error = $"Installed gem '{socket.GemLootId}' is not present in the loot catalog.";
            return false;
        }

        catalogIndexPlusOne = catalogIndex + 1;
        return true;
    }

    private static bool TryDecodeGem(
        int catalogIndexPlusOne,
        LootDefinitionCatalog catalog,
        out WeaponGemSocket socket,
        out string error)
    {
        socket = default;
        error = null;
        if (catalogIndexPlusOne == 0)
        {
            return true;
        }

        if (!catalog.TryGetByIndex(catalogIndexPlusOne - 1, out LootDefinition gem) || gem == null)
        {
            error = $"Installed gem catalog index '{catalogIndexPlusOne - 1}' is not present in the loot catalog.";
            return false;
        }

        return WeaponGemSocket.TryCreateOccupied(gem.LootId, out socket, out error);
    }

    private static bool TryDecodeModifier(
        byte attribute,
        byte grade,
        out WeaponScalingModifier modifier,
        out string error)
    {
        modifier = default;
        error = null;
        if (grade == (byte)WeaponScalingGrade.None)
        {
            return true;
        }

        return WeaponScalingModifier.TryCreate(
            (CharacterAttribute)attribute,
            (WeaponScalingGrade)grade,
            out modifier,
            out error);
    }
}
