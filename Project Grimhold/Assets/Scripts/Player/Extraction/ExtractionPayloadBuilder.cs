using System.Collections.Generic;

/// <summary>
/// Builds the extraction stash items and prepared equipment from the wire arrays shared by the State
/// Authority publish and the Input Authority local commit, so both sides derive the same unit set
/// (ItemInstanceArchitecture sections 6.3 and 7). Equipment travels as amount-1 units: units that
/// carry modifiers become ID-less instances, plain units stay plain.
/// </summary>
internal static class ExtractionPayloadBuilder
{
    internal const int SlotCount = 8;
    internal const int HandCount = 4;

    // Order of the prepared-equipment slot indices carried by the RPC.
    private static readonly EquipmentSlot[] SlotOrder =
    {
        EquipmentSlot.WeaponSetAMainHand, EquipmentSlot.WeaponSetBMainHand, EquipmentSlot.Helmet,
        EquipmentSlot.Armor, EquipmentSlot.Gloves, EquipmentSlot.Boots,
        EquipmentSlot.WeaponSetAOffHand, EquipmentSlot.WeaponSetBOffHand
    };

    // Slots that can carry modifiers, in the order of the hand-modifier words.
    internal static readonly EquipmentSlot[] HandSlots =
    {
        EquipmentSlot.WeaponSetAMainHand, EquipmentSlot.WeaponSetBMainHand,
        EquipmentSlot.WeaponSetAOffHand, EquipmentSlot.WeaponSetBOffHand
    };

    internal static bool TryBuildItems(
        LootDefinitionCatalog catalog,
        int[] catalogIndices,
        int[] amounts,
        int[] modifiedIndices,
        int[] modifiedPacked,
        out List<StashItem> items)
    {
        items = new List<StashItem>();
        modifiedIndices ??= System.Array.Empty<int>();
        modifiedPacked ??= System.Array.Empty<int>();
        if (catalog == null || catalogIndices == null || amounts == null ||
            catalogIndices.Length != amounts.Length || modifiedIndices.Length != modifiedPacked.Length)
        {
            return false;
        }

        int consumed = 0;
        for (int entry = 0; entry < catalogIndices.Length; entry++)
        {
            if (!catalog.TryGetByIndex(catalogIndices[entry], out LootDefinition definition) ||
                definition == null || amounts[entry] <= 0)
            {
                return false;
            }

            if (!EquipmentInstance.IsEquipment(definition))
            {
                if (HasModified(modifiedIndices, catalogIndices[entry]))
                {
                    return false;
                }

                items.Add(new StashItem(definition.LootId, amounts[entry]));
                continue;
            }

            int modifiedCount = 0;
            for (int unit = 0; unit < modifiedIndices.Length; unit++)
            {
                if (modifiedIndices[unit] != catalogIndices[entry])
                {
                    continue;
                }

                if (++modifiedCount > amounts[entry] ||
                    !TryResolve(catalog, definition, modifiedPacked[unit], out WeaponInstanceModifiers modifiers))
                {
                    return false;
                }

                items.Add(new StashItem(definition.LootId, 1, -1, new EquipmentInstance(null, modifiers)));
            }

            consumed += modifiedCount;
            for (int plain = amounts[entry] - modifiedCount; plain > 0; plain--)
            {
                items.Add(new StashItem(definition.LootId, 1));
            }
        }

        return consumed == modifiedIndices.Length;
    }

    /// <param name="slotIndices">Catalog index or -1 per slot, in <see cref="SlotOrder"/> order.</param>
    /// <param name="handPacked">Packed modifiers of the four hand slots, in <see cref="HandSlots"/> order.</param>
    internal static bool TryBuildPrepared(
        LootDefinitionCatalog catalog,
        int[] slotIndices,
        int[] handPacked,
        out PreparedEquipmentLoadout prepared)
    {
        prepared = default;
        if (catalog == null || slotIndices == null || slotIndices.Length != SlotCount ||
            handPacked == null || handPacked.Length != HandCount)
        {
            return false;
        }

        var ids = new LootId[SlotCount];
        var instances = new EquipmentInstance[SlotCount];
        for (int slot = 0; slot < SlotCount; slot++)
        {
            int handIndex = System.Array.IndexOf(HandSlots, SlotOrder[slot]);
            int packed = handIndex >= 0 ? handPacked[handIndex] : 0;
            if (slotIndices[slot] < 0)
            {
                if (packed != 0)
                {
                    return false;
                }

                continue;
            }

            if (!catalog.TryGetByIndex(slotIndices[slot], out LootDefinition definition) || definition == null)
            {
                return false;
            }

            ids[slot] = definition.LootId;
            if (packed == 0)
            {
                continue;
            }

            if (!TryResolve(catalog, definition, packed, out WeaponInstanceModifiers modifiers))
            {
                return false;
            }

            instances[slot] = new EquipmentInstance(null, modifiers);
        }

        prepared = new PreparedEquipmentLoadout(
            ids[0], ids[1], ids[2], ids[3], ids[4], ids[5], ids[6], ids[7],
            instances[0], instances[1], instances[2], instances[3], instances[4], instances[5], instances[6], instances[7]);
        return true;
    }

    private static bool HasModified(int[] modifiedIndices, int catalogIndex)
    {
        for (int index = 0; index < modifiedIndices.Length; index++)
        {
            if (modifiedIndices[index] == catalogIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolve(
        LootDefinitionCatalog catalog,
        LootDefinition definition,
        int packed,
        out WeaponInstanceModifiers modifiers)
    {
        modifiers = default;
        return packed != 0 && definition.Category == LootCategory.Weapon && definition.WeaponDefinition != null &&
            WeaponInstanceModifiersNetworkData.FromPacked(packed).TryResolve(
                catalog, definition.WeaponDefinition.NaturalScalingAttribute, out modifiers, out _);
    }
}
