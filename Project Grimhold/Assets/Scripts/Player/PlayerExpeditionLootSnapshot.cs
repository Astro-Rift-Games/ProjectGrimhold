using System;
using System.Collections.Generic;

/// <summary>
/// Captures complete Raid loot ownership split between Inventory and the eight Equipment slots,
/// while exposing an aggregated snapshot to persistence and corpse flows.
/// </summary>
public sealed class PlayerExpeditionLootSnapshot
{
    private PlayerExpeditionLootSnapshot(
        IReadOnlyList<LootEntry> inventory,
        LootEntry? weaponSetAMainHand,
        LootEntry? weaponSetBMainHand,
        LootEntry? helmet,
        LootEntry? armor,
        LootEntry? gloves,
        LootEntry? boots,
        LootEntry? weaponSetAOffHand,
        LootEntry? weaponSetBOffHand,
        IReadOnlyList<LootEntry> combined,
        IReadOnlyList<RaidLootOriginEntry> inventoryOrigins,
        RaidLootOrigin? weaponSetAMainHandOrigin,
        RaidLootOrigin? weaponSetBMainHandOrigin,
        RaidLootOrigin? helmetOrigin,
        RaidLootOrigin? armorOrigin,
        RaidLootOrigin? glovesOrigin,
        RaidLootOrigin? bootsOrigin,
        RaidLootOrigin? weaponSetAOffHandOrigin,
        RaidLootOrigin? weaponSetBOffHandOrigin,
        IReadOnlyList<RaidLootOriginEntry> combinedOrigins)
    {
        Inventory = inventory;
        WeaponSetAMainHand = weaponSetAMainHand;
        WeaponSetBMainHand = weaponSetBMainHand;
        Helmet = helmet;
        Armor = armor;
        Gloves = gloves;
        Boots = boots;
        WeaponSetAOffHand = weaponSetAOffHand;
        WeaponSetBOffHand = weaponSetBOffHand;
        Combined = combined;
        InventoryOrigins = inventoryOrigins;
        WeaponSetAMainHandOrigin = weaponSetAMainHandOrigin;
        WeaponSetBMainHandOrigin = weaponSetBMainHandOrigin;
        HelmetOrigin = helmetOrigin;
        ArmorOrigin = armorOrigin;
        GlovesOrigin = glovesOrigin;
        BootsOrigin = bootsOrigin;
        WeaponSetAOffHandOrigin = weaponSetAOffHandOrigin;
        WeaponSetBOffHandOrigin = weaponSetBOffHandOrigin;
        CombinedOrigins = combinedOrigins;
    }

    public IReadOnlyList<LootEntry> Inventory { get; }
    public LootEntry? WeaponSetAMainHand { get; }
    public LootEntry? WeaponSetBMainHand { get; }
    public LootEntry? Helmet { get; }
    public LootEntry? Armor { get; }
    public LootEntry? Gloves { get; }
    public LootEntry? Boots { get; }
    public LootEntry? WeaponSetAOffHand { get; }
    public LootEntry? WeaponSetBOffHand { get; }
    public IReadOnlyList<LootEntry> Combined { get; }
    public IReadOnlyList<RaidLootOriginEntry> InventoryOrigins { get; }
    public RaidLootOrigin? WeaponSetAMainHandOrigin { get; }
    public RaidLootOrigin? WeaponSetBMainHandOrigin { get; }
    public RaidLootOrigin? HelmetOrigin { get; }
    public RaidLootOrigin? ArmorOrigin { get; }
    public RaidLootOrigin? GlovesOrigin { get; }
    public RaidLootOrigin? BootsOrigin { get; }
    public RaidLootOrigin? WeaponSetAOffHandOrigin { get; }
    public RaidLootOrigin? WeaponSetBOffHandOrigin { get; }
    public IReadOnlyList<RaidLootOriginEntry> CombinedOrigins { get; }

    public static bool TryCapture(
        PlayerLootReceiver lootReceiver,
        PlayerWeaponEquipmentNetworkController equipment,
        out PlayerExpeditionLootSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = null;
        if (lootReceiver == null || equipment == null ||
            !lootReceiver.TryGetLootContent(out IReadOnlyList<LootEntry> inventory) ||
            !lootReceiver.TryGetRaidLootOriginEntries(out IReadOnlyList<RaidLootOriginEntry> inventoryOrigins))
        {
            error = "Inventory or equipment dependencies are unavailable.";
            return false;
        }

        LootEntry? weaponSetAMainHand = CaptureSlot(equipment, EquipmentSlot.WeaponSetAMainHand);
        LootEntry? weaponSetBMainHand = CaptureSlot(equipment, EquipmentSlot.WeaponSetBMainHand);
        LootEntry? helmet = CaptureSlot(equipment, EquipmentSlot.Helmet);
        LootEntry? armor = CaptureSlot(equipment, EquipmentSlot.Armor);
        LootEntry? gloves = CaptureSlot(equipment, EquipmentSlot.Gloves);
        LootEntry? boots = CaptureSlot(equipment, EquipmentSlot.Boots);
        LootEntry? weaponSetAOffHand = CaptureSlot(equipment, EquipmentSlot.WeaponSetAOffHand);
        LootEntry? weaponSetBOffHand = CaptureSlot(equipment, EquipmentSlot.WeaponSetBOffHand);
        RaidLootOrigin? weaponSetAMainHandOrigin = CaptureSlotOrigin(equipment, EquipmentSlot.WeaponSetAMainHand);
        RaidLootOrigin? weaponSetBMainHandOrigin = CaptureSlotOrigin(equipment, EquipmentSlot.WeaponSetBMainHand);
        RaidLootOrigin? helmetOrigin = CaptureSlotOrigin(equipment, EquipmentSlot.Helmet);
        RaidLootOrigin? armorOrigin = CaptureSlotOrigin(equipment, EquipmentSlot.Armor);
        RaidLootOrigin? glovesOrigin = CaptureSlotOrigin(equipment, EquipmentSlot.Gloves);
        RaidLootOrigin? bootsOrigin = CaptureSlotOrigin(equipment, EquipmentSlot.Boots);
        RaidLootOrigin? weaponSetAOffHandOrigin = CaptureSlotOrigin(equipment, EquipmentSlot.WeaponSetAOffHand);
        RaidLootOrigin? weaponSetBOffHandOrigin = CaptureSlotOrigin(equipment, EquipmentSlot.WeaponSetBOffHand);

        var combined = new List<LootEntry>(inventory.Count + PlayerWeaponEquipmentNetworkController.AllSlots.Length);
        combined.AddRange(inventory);

        if (!TryMerge(combined, weaponSetAMainHand, out error) ||
            !TryMerge(combined, weaponSetBMainHand, out error) ||
            !TryMerge(combined, helmet, out error) ||
            !TryMerge(combined, armor, out error) ||
            !TryMerge(combined, gloves, out error) ||
            !TryMerge(combined, boots, out error) ||
            !TryMerge(combined, weaponSetAOffHand, out error) ||
            !TryMerge(combined, weaponSetBOffHand, out error))
        {
            return false;
        }

        if (!TryValidateOriginTotals(inventory, inventoryOrigins, out error))
        {
            return false;
        }

        var combinedOrigins = new List<RaidLootOriginEntry>(
            inventoryOrigins.Count + PlayerWeaponEquipmentNetworkController.AllSlots.Length);
        combinedOrigins.AddRange(inventoryOrigins);
        if (!TryMergeOrigin(combinedOrigins, weaponSetAMainHand, weaponSetAMainHandOrigin, out error) ||
            !TryMergeOrigin(combinedOrigins, weaponSetBMainHand, weaponSetBMainHandOrigin, out error) ||
            !TryMergeOrigin(combinedOrigins, helmet, helmetOrigin, out error) ||
            !TryMergeOrigin(combinedOrigins, armor, armorOrigin, out error) ||
            !TryMergeOrigin(combinedOrigins, gloves, glovesOrigin, out error) ||
            !TryMergeOrigin(combinedOrigins, boots, bootsOrigin, out error) ||
            !TryMergeOrigin(combinedOrigins, weaponSetAOffHand, weaponSetAOffHandOrigin, out error) ||
            !TryMergeOrigin(combinedOrigins, weaponSetBOffHand, weaponSetBOffHandOrigin, out error))
        {
            return false;
        }

        combinedOrigins.Sort((left, right) =>
        {
            if (!lootReceiver.TryGetCatalogIndex(left.LootId, out int leftIndex) ||
                !lootReceiver.TryGetCatalogIndex(right.LootId, out int rightIndex))
            {
                return string.CompareOrdinal(left.LootId.Value, right.LootId.Value);
            }

            int catalogOrder = leftIndex.CompareTo(rightIndex);
            return catalogOrder != 0 ? catalogOrder : left.Origin.CompareTo(right.Origin);
        });

        snapshot = new PlayerExpeditionLootSnapshot(
            inventory,
            weaponSetAMainHand,
            weaponSetBMainHand,
            helmet,
            armor,
            gloves,
            boots,
            weaponSetAOffHand,
            weaponSetBOffHand,
            combined.AsReadOnly(),
            inventoryOrigins,
            weaponSetAMainHandOrigin,
            weaponSetBMainHandOrigin,
            helmetOrigin,
            armorOrigin,
            glovesOrigin,
            bootsOrigin,
            weaponSetAOffHandOrigin,
            weaponSetBOffHandOrigin,
            combinedOrigins.AsReadOnly());
        return true;
    }

    public bool MatchesCurrent(
        PlayerLootReceiver lootReceiver,
        PlayerWeaponEquipmentNetworkController equipment,
        out string error)
    {
        if (!lootReceiver.TryMatchesExactRaidContent(Inventory, InventoryOrigins, out error))
        {
            return false;
        }

        return equipment.TryMatchesExactEquipment(
                WeaponSetAMainHand, WeaponSetBMainHand, Helmet, Armor, Gloves, Boots,
                WeaponSetAOffHand, WeaponSetBOffHand, out error) &&
            equipment.TryMatchesExactEquipmentOrigins(
                WeaponSetAMainHandOrigin, WeaponSetBMainHandOrigin, HelmetOrigin,
                ArmorOrigin, GlovesOrigin, BootsOrigin,
                WeaponSetAOffHandOrigin, WeaponSetBOffHandOrigin, out error);
    }

    public bool TryClearExact(
        PlayerLootReceiver lootReceiver,
        PlayerWeaponEquipmentNetworkController equipment,
        out string error)
    {
        if (!MatchesCurrent(lootReceiver, equipment, out error))
        {
            return false;
        }

        if (!lootReceiver.TryClearExactRaidContent(Inventory, InventoryOrigins, out error))
        {
            return false;
        }

        if (!equipment.TryClearExactEquipmentOrigins(
                WeaponSetAMainHandOrigin, WeaponSetBMainHandOrigin, HelmetOrigin,
                ArmorOrigin, GlovesOrigin, BootsOrigin,
                WeaponSetAOffHandOrigin, WeaponSetBOffHandOrigin, out error))
        {
            throw new InvalidOperationException(error ?? "Validated Equipment provenance could not be cleared.");
        }

        return equipment.TryClearExactEquipment(
            WeaponSetAMainHand, WeaponSetBMainHand, Helmet, Armor, Gloves, Boots,
            WeaponSetAOffHand, WeaponSetBOffHand, out error);
    }

    private static LootEntry? CaptureSlot(
        PlayerWeaponEquipmentNetworkController equipment,
        EquipmentSlot slot) =>
        equipment.TryGetSlotLoot(slot, out LootEntry entry) ? entry : null;

    private static RaidLootOrigin? CaptureSlotOrigin(
        PlayerWeaponEquipmentNetworkController equipment,
        EquipmentSlot slot) =>
        equipment.TryGetSlotRaidOrigin(slot, out RaidLootOrigin origin) ? origin : null;

    private static bool TryMergeOrigin(
        List<RaidLootOriginEntry> combined,
        LootEntry? loot,
        RaidLootOrigin? origin,
        out string error)
    {
        error = null;
        if (!loot.HasValue)
        {
            return !origin.HasValue;
        }

        if (!origin.HasValue)
        {
            error = "Occupied Equipment slot has no Raid provenance.";
            return false;
        }

        for (int index = 0; index < combined.Count; index++)
        {
            RaidLootOriginEntry current = combined[index];
            if (current.LootId != loot.Value.LootId || current.Origin != origin.Value)
            {
                continue;
            }

            try
            {
                combined[index] = new RaidLootOriginEntry(
                    current.LootId,
                    current.Origin,
                    checked(current.Amount + loot.Value.Amount));
                return true;
            }
            catch (OverflowException)
            {
                error = "Combined expedition provenance amount overflowed.";
                return false;
            }
        }

        combined.Add(new RaidLootOriginEntry(loot.Value.LootId, origin.Value, loot.Value.Amount));
        return true;
    }

    internal static bool TryValidateOriginTotals(
        IReadOnlyList<LootEntry> loot,
        IReadOnlyList<RaidLootOriginEntry> origins,
        out string error)
    {
        error = null;
        if (loot == null || origins == null)
        {
            error = "Loot and Raid provenance snapshots are required.";
            return false;
        }

        var expected = new Dictionary<LootId, int>(loot.Count);
        for (int index = 0; index < loot.Count; index++)
        {
            LootEntry entry = loot[index];
            if (!entry.IsValid || expected.ContainsKey(entry.LootId))
            {
                error = "Loot snapshot contains an invalid or duplicate entry.";
                return false;
            }
            expected.Add(entry.LootId, entry.Amount);
        }

        var actual = new Dictionary<LootId, int>(loot.Count);
        var distinct = new HashSet<(LootId LootId, RaidLootOrigin Origin)>();
        try
        {
            for (int index = 0; index < origins.Count; index++)
            {
                RaidLootOriginEntry entry = origins[index];
                if (!entry.IsValid || !expected.ContainsKey(entry.LootId) ||
                    !distinct.Add((entry.LootId, entry.Origin)))
                {
                    error = "Raid provenance snapshot contains an invalid, duplicate, or unexpected bucket.";
                    return false;
                }

                actual.TryGetValue(entry.LootId, out int current);
                actual[entry.LootId] = checked(current + entry.Amount);
            }
        }
        catch (OverflowException)
        {
            error = "Raid provenance snapshot quantity overflowed.";
            return false;
        }

        if (actual.Count != expected.Count)
        {
            error = "Raid provenance does not cover every Loot entry.";
            return false;
        }

        foreach (KeyValuePair<LootId, int> expectedEntry in expected)
        {
            if (!actual.TryGetValue(expectedEntry.Key, out int actualAmount) ||
                actualAmount != expectedEntry.Value)
            {
                error = "Raid provenance quantities do not match Loot quantities.";
                return false;
            }
        }

        return true;
    }

    private static bool TryMerge(
        List<LootEntry> combined,
        LootEntry? incoming,
        out string error)
    {
        error = null;
        if (!incoming.HasValue)
        {
            return true;
        }

        for (int index = 0; index < combined.Count; index++)
        {
            if (combined[index].LootId != incoming.Value.LootId)
            {
                continue;
            }

            try
            {
                combined[index] = new LootEntry(
                    combined[index].LootId,
                    checked(combined[index].Amount + incoming.Value.Amount));
                return true;
            }
            catch (OverflowException)
            {
                error = "Combined expedition loot amount overflowed.";
                return false;
            }
        }

        combined.Add(incoming.Value);
        return true;
    }
}
