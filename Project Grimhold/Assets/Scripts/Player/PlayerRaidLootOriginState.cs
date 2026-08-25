using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>Replicated Raid-only provenance for one player's Inventory and Equipment.</summary>
[DisallowMultipleComponent]
public sealed class PlayerRaidLootOriginState : NetworkBehaviour
{
    [Networked]
    private RaidLootOriginPackedState InventoryOrigins { get; set; }

    // Six 5-bit stable origin slots, one for each EquipmentSlotRules.AllSlots entry.
    [Networked]
    private int EquipmentOriginSlots { get; set; }

    public bool TryInitializePlayerLoadout(
        IReadOnlyList<LootEntry> entries,
        LootDefinitionCatalog catalog,
        RaidParticipantId participantId,
        out string error)
    {
        error = null;
        RaidLootOriginPackedState state = InventoryOrigins;
        if (!HasStateAuthority || entries == null || catalog == null || state.BucketCount != 0 ||
            !RaidLootOriginTransfer.TryCreatePlayer(participantId, 1, out _))
        {
            error = "Player Raid origin initialization requires empty authoritative state and a valid RaidParticipantId.";
            return false;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            LootEntry entry = entries[index];
            if (!entry.IsValid || !catalog.TryGetIndex(entry.LootId, out int catalogIndex) ||
                !RaidLootOriginTransfer.TryCreatePlayer(participantId, entry.Amount, out RaidLootOriginTransfer transfer) ||
                !RaidLootOriginIndexedStateUtility.TryAdd(
                    ref state,
                    catalogIndex,
                    transfer))
            {
                error = "Loadout provenance cannot be represented by the Raid origin state.";
                return false;
            }
        }

        InventoryOrigins = state;
        return true;
    }

    public bool HasExactInventoryTotal(int catalogIndex, int expectedAmount) =>
        RaidLootOriginIndexedStateUtility.HasExactTotal(
            InventoryOrigins,
            catalogIndex,
            expectedAmount);

    public bool TryResolveInventoryTransfer(
        int catalogIndex,
        int amount,
        out RaidLootOriginTransfer transfer) =>
        RaidLootOriginIndexedStateUtility.TryResolveTransfer(
            InventoryOrigins,
            catalogIndex,
            amount,
            out transfer);

    public bool CanReceiveInventory(int catalogIndex, RaidLootOriginTransfer transfer) =>
        HasStateAuthority && RaidLootOriginIndexedStateUtility.CanAdd(
            InventoryOrigins,
            catalogIndex,
            transfer);

    public void CommitReceiveInventory(int catalogIndex, RaidLootOriginTransfer transfer)
    {
        RaidLootOriginPackedState state = InventoryOrigins;
        if (!HasStateAuthority || !RaidLootOriginIndexedStateUtility.TryAdd(
                ref state,
                catalogIndex,
                transfer))
        {
            throw new InvalidOperationException("Validated Inventory provenance reception could not be committed.");
        }

        InventoryOrigins = state;
    }

    public void CommitExtractInventory(int catalogIndex, RaidLootOriginTransfer transfer)
    {
        RaidLootOriginPackedState state = InventoryOrigins;
        if (!HasStateAuthority || !RaidLootOriginIndexedStateUtility.TryRemove(
                ref state,
                catalogIndex,
                transfer))
        {
            throw new InvalidOperationException("Validated Inventory provenance extraction could not be committed.");
        }

        InventoryOrigins = state;
    }

    public bool TryGetInventoryEntries(
        LootDefinitionCatalog catalog,
        out IReadOnlyList<RaidLootOriginEntry> entries) =>
        RaidLootOriginIndexedStateUtility.TryGetEntries(
            InventoryOrigins,
            catalog,
            out entries);

    public bool TrySetEquipmentOrigin(EquipmentSlot slot, RaidLootOrigin origin)
    {
        if (!HasStateAuthority || !TryGetEquipmentIndex(slot, out int equipmentIndex) ||
            GetEquipmentOriginSlot(equipmentIndex) != 0 ||
            !TryGetOrInternOriginSlot(origin, out int originSlot))
        {
            return false;
        }

        SetEquipmentOriginSlot(equipmentIndex, originSlot);
        return true;
    }

    public bool TryGetEquipmentOrigin(EquipmentSlot slot, out RaidLootOrigin origin)
    {
        origin = default;
        if (!TryGetEquipmentIndex(slot, out int equipmentIndex))
        {
            return false;
        }

        int originSlot = GetEquipmentOriginSlot(equipmentIndex);
        if (originSlot == 0)
        {
            origin = RaidLootOrigin.Dungeon;
            return true;
        }

        return TryResolveOriginSlot(originSlot, out origin);
    }

    public bool TryClearEquipmentOrigin(EquipmentSlot slot, RaidLootOrigin expected)
    {
        if (!HasStateAuthority || !TryGetEquipmentIndex(slot, out int equipmentIndex))
        {
            return false;
        }

        int originSlot = GetEquipmentOriginSlot(equipmentIndex);
        if (originSlot == 0 ? !expected.IsDungeon :
            !TryResolveOriginSlot(originSlot, out RaidLootOrigin current) || current != expected)
        {
            return false;
        }

        SetEquipmentOriginSlot(equipmentIndex, 0);
        return true;
    }

    public bool TryClearExactInventory(
        IReadOnlyList<RaidLootOriginEntry> expected,
        LootDefinitionCatalog catalog,
        out string error)
    {
        error = null;
        if (!HasStateAuthority || !TryGetInventoryEntries(catalog, out IReadOnlyList<RaidLootOriginEntry> current) ||
            !AreEqual(expected, current))
        {
            error = "Inventory provenance differs from the expected snapshot.";
            return false;
        }

        RaidLootOriginPackedState state = InventoryOrigins;
        for (int index = state.BucketCount - 1; index >= 0; index--)
        {
            RaidLootOriginPackedBuffer.Clear(ref state, index);
        }
        state.BucketCount = 0;
        InventoryOrigins = state;
        return true;
    }

    private bool TryGetOrInternOriginSlot(RaidLootOrigin origin, out int originSlot)
    {
        originSlot = 0;
        if (origin.IsDungeon)
        {
            return true;
        }

        if (!origin.IsPlayer)
        {
            return false;
        }

        originSlot = origin.PlayerParticipantId.Value;
        return originSlot >= 1 && originSlot <= RaidLootOriginPackedBuffer.MaximumPlayerOrigins;
    }

    private bool TryResolveOriginSlot(int originSlot, out RaidLootOrigin origin)
    {
        origin = default;
        if (originSlot == 0)
        {
            return false;
        }

        return RaidParticipantId.TryCreate(originSlot, out RaidParticipantId participantId) &&
            RaidLootOrigin.TryCreatePlayer(participantId, out origin);
    }

    private int GetEquipmentOriginSlot(int equipmentIndex) =>
        (EquipmentOriginSlots >> (equipmentIndex * 5)) & 31;

    private void SetEquipmentOriginSlot(int equipmentIndex, int originSlot)
    {
        int shift = equipmentIndex * 5;
        int mask = 31 << shift;
        EquipmentOriginSlots = (EquipmentOriginSlots & ~mask) | (originSlot << shift);
    }

    private static bool TryGetEquipmentIndex(EquipmentSlot slot, out int equipmentIndex)
    {
        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        for (int index = 0; index < slots.Length; index++)
        {
            if (slots[index] == slot)
            {
                equipmentIndex = index;
                return true;
            }
        }

        equipmentIndex = -1;
        return false;
    }

    private static bool AreEqual(
        IReadOnlyList<RaidLootOriginEntry> expected,
        IReadOnlyList<RaidLootOriginEntry> current)
    {
        if (expected == null || current == null || expected.Count != current.Count)
        {
            return false;
        }

        for (int index = 0; index < expected.Count; index++)
        {
            if (!expected[index].Equals(current[index]))
            {
                return false;
            }
        }

        return true;
    }
}
