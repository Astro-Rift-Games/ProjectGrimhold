using System;
using System.Collections.Generic;

/// <summary>
/// Captures complete Raid loot ownership split between Inventory and the six Equipment slots,
/// while exposing an aggregated snapshot to persistence and corpse flows.
/// </summary>
public sealed class PlayerExpeditionLootSnapshot
{
    private PlayerExpeditionLootSnapshot(
        IReadOnlyList<LootEntry> inventory,
        LootEntry? weaponSlot1,
        LootEntry? weaponSlot2,
        LootEntry? helmet,
        LootEntry? armor,
        LootEntry? gloves,
        LootEntry? boots,
        IReadOnlyList<LootEntry> combined)
    {
        Inventory = inventory;
        WeaponSlot1 = weaponSlot1;
        WeaponSlot2 = weaponSlot2;
        Helmet = helmet;
        Armor = armor;
        Gloves = gloves;
        Boots = boots;
        Combined = combined;
    }

    public IReadOnlyList<LootEntry> Inventory { get; }
    public LootEntry? WeaponSlot1 { get; }
    public LootEntry? WeaponSlot2 { get; }
    public LootEntry? Helmet { get; }
    public LootEntry? Armor { get; }
    public LootEntry? Gloves { get; }
    public LootEntry? Boots { get; }
    public IReadOnlyList<LootEntry> Combined { get; }

    public static bool TryCapture(
        PlayerLootReceiver lootReceiver,
        PlayerWeaponEquipmentNetworkController equipment,
        out PlayerExpeditionLootSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = null;
        if (lootReceiver == null || equipment == null ||
            !lootReceiver.TryGetLootContent(out IReadOnlyList<LootEntry> inventory))
        {
            error = "Inventory or equipment dependencies are unavailable.";
            return false;
        }

        LootEntry? weaponSlot1 = CaptureSlot(equipment, EquipmentSlot.WeaponSlot1);
        LootEntry? weaponSlot2 = CaptureSlot(equipment, EquipmentSlot.WeaponSlot2);
        LootEntry? helmet = CaptureSlot(equipment, EquipmentSlot.Helmet);
        LootEntry? armor = CaptureSlot(equipment, EquipmentSlot.Armor);
        LootEntry? gloves = CaptureSlot(equipment, EquipmentSlot.Gloves);
        LootEntry? boots = CaptureSlot(equipment, EquipmentSlot.Boots);

        var combined = new List<LootEntry>(inventory.Count + PlayerWeaponEquipmentNetworkController.AllSlots.Length);
        combined.AddRange(inventory);

        if (!TryMerge(combined, weaponSlot1, out error) ||
            !TryMerge(combined, weaponSlot2, out error) ||
            !TryMerge(combined, helmet, out error) ||
            !TryMerge(combined, armor, out error) ||
            !TryMerge(combined, gloves, out error) ||
            !TryMerge(combined, boots, out error))
        {
            return false;
        }

        snapshot = new PlayerExpeditionLootSnapshot(
            inventory,
            weaponSlot1,
            weaponSlot2,
            helmet,
            armor,
            gloves,
            boots,
            combined.AsReadOnly());
        return true;
    }

    public bool MatchesCurrent(
        PlayerLootReceiver lootReceiver,
        PlayerWeaponEquipmentNetworkController equipment,
        out string error)
    {
        if (!lootReceiver.TryMatchesExactContent(Inventory, out error))
        {
            return false;
        }

        return equipment.TryMatchesExactEquipment(
            WeaponSlot1, WeaponSlot2, Helmet, Armor, Gloves, Boots, out error);
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

        if (!lootReceiver.TryClearExactContent(Inventory, out error))
        {
            return false;
        }

        return equipment.TryClearExactEquipment(
            WeaponSlot1, WeaponSlot2, Helmet, Armor, Gloves, Boots, out error);
    }

    private static LootEntry? CaptureSlot(
        PlayerWeaponEquipmentNetworkController equipment,
        EquipmentSlot slot) =>
        equipment.TryGetSlotLoot(slot, out LootEntry entry) ? entry : null;

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
