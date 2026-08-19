using System;
using System.Collections.Generic;

/// <summary>
/// Captures the complete Raid loot ownership split between inventory and the single
/// equipped weapon, while exposing an aggregated snapshot to existing persistence/container flows.
/// </summary>
public sealed class PlayerExpeditionLootSnapshot
{
    private PlayerExpeditionLootSnapshot(
        IReadOnlyList<LootEntry> inventory,
        LootEntry? equippedWeapon,
        IReadOnlyList<LootEntry> combined)
    {
        Inventory = inventory;
        EquippedWeapon = equippedWeapon;
        Combined = combined;
    }

    public IReadOnlyList<LootEntry> Inventory { get; }
    public LootEntry? EquippedWeapon { get; }
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

        LootEntry? equippedWeapon = equipment.TryGetEquippedLoot(out LootEntry equipped)
            ? equipped
            : null;
        var combined = new List<LootEntry>(inventory.Count + (equippedWeapon.HasValue ? 1 : 0));
        combined.AddRange(inventory);

        if (equippedWeapon.HasValue)
        {
            bool merged = false;
            for (int i = 0; i < combined.Count; i++)
            {
                if (combined[i].LootId != equippedWeapon.Value.LootId)
                {
                    continue;
                }

                try
                {
                    combined[i] = new LootEntry(
                        combined[i].LootId,
                        checked(combined[i].Amount + equippedWeapon.Value.Amount));
                }
                catch (OverflowException)
                {
                    error = "Combined expedition loot amount overflowed.";
                    return false;
                }

                merged = true;
                break;
            }

            if (!merged)
            {
                combined.Add(equippedWeapon.Value);
            }
        }

        snapshot = new PlayerExpeditionLootSnapshot(
            inventory,
            equippedWeapon,
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

        return equipment.TryMatchesExactEquippedLoot(EquippedWeapon, out error);
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

        return !EquippedWeapon.HasValue ||
            equipment.TryClearExactEquippedLoot(EquippedWeapon.Value, out error);
    }
}
