using System;
using System.Collections.Generic;

/// <summary>
/// Captures complete Raid loot ownership split between Inventory and both weapon slots,
/// while exposing an aggregated snapshot to persistence and corpse flows.
/// </summary>
public sealed class PlayerExpeditionLootSnapshot
{
    private PlayerExpeditionLootSnapshot(
        IReadOnlyList<LootEntry> inventory,
        LootEntry? weaponSlot1,
        LootEntry? weaponSlot2,
        IReadOnlyList<LootEntry> combined)
    {
        Inventory = inventory;
        WeaponSlot1 = weaponSlot1;
        WeaponSlot2 = weaponSlot2;
        Combined = combined;
    }

    public IReadOnlyList<LootEntry> Inventory { get; }
    public LootEntry? WeaponSlot1 { get; }
    public LootEntry? WeaponSlot2 { get; }
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

        LootEntry? weaponSlot1 = equipment.TryGetSlotLoot(WeaponSlot.Slot1, out LootEntry slot1)
            ? slot1
            : null;
        LootEntry? weaponSlot2 = equipment.TryGetSlotLoot(WeaponSlot.Slot2, out LootEntry slot2)
            ? slot2
            : null;
        var combined = new List<LootEntry>(
            inventory.Count + (weaponSlot1.HasValue ? 1 : 0) + (weaponSlot2.HasValue ? 1 : 0));
        combined.AddRange(inventory);

        if (!TryMerge(combined, weaponSlot1, out error) ||
            !TryMerge(combined, weaponSlot2, out error))
        {
            return false;
        }

        snapshot = new PlayerExpeditionLootSnapshot(
            inventory,
            weaponSlot1,
            weaponSlot2,
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

        return equipment.TryMatchesExactEquipment(WeaponSlot1, WeaponSlot2, out error);
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

        return equipment.TryClearExactEquipment(WeaponSlot1, WeaponSlot2, out error);
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
