using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Replicates only Raid Equipment origins. Inventory queries are a compatibility facade
/// over PlayerLootReceiver's physical slots; no Inventory provenance is stored here.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRaidLootOriginState : NetworkBehaviour
{
    private PlayerLootReceiver _lootReceiver;

    private void Awake() => _lootReceiver = GetComponent<PlayerLootReceiver>();

    // Eight 5-bit stable origin slots, one for each EquipmentSlotRules.AllSlots entry.
    [Networked]
    private long EquipmentOriginSlots { get; set; }

    public bool HasExactInventoryTotal(int catalogIndex, int expectedAmount) =>
        expectedAmount > 0 && _lootReceiver != null && _lootReceiver.LootCatalog != null &&
        _lootReceiver.LootCatalog.TryGetByIndex(catalogIndex, out LootDefinition definition) &&
        _lootReceiver.GetLootAmount(definition.LootId) == expectedAmount;

    public bool TryResolveInventoryTransfer(
        int catalogIndex,
        int amount,
        out RaidLootOriginTransfer transfer,
        RaidLootOriginSelectionOrder selectionOrder = RaidLootOriginSelectionOrder.Canonical)
    {
        transfer = null;
        return _lootReceiver != null &&
            _lootReceiver.TryResolveInventoryTransfer(catalogIndex, amount, selectionOrder, out transfer);
    }

    public bool TryGetInventoryEntries(
        LootDefinitionCatalog catalog,
        out IReadOnlyList<RaidLootOriginEntry> entries)
    {
        entries = System.Array.Empty<RaidLootOriginEntry>();
        return _lootReceiver != null && catalog != null && catalog == _lootReceiver.LootCatalog &&
            _lootReceiver.TryGetRaidLootOriginEntries(out entries);
    }

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
        (int)((EquipmentOriginSlots >> (equipmentIndex * 5)) & 31L);

    private void SetEquipmentOriginSlot(int equipmentIndex, int originSlot)
    {
        int shift = equipmentIndex * 5;
        long mask = 31L << shift;
        EquipmentOriginSlots = (EquipmentOriginSlots & ~mask) | ((long)originSlot << shift);
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
}
