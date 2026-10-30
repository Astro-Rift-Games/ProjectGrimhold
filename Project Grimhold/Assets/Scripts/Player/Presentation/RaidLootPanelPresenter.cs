using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Projects one read-only loot source into reusable slot presentation data.
/// </summary>
public sealed class RaidLootPanelPresenter
{
    private readonly List<LootEntry> _projectedEntries = new();
    private readonly List<RaidInventorySlotData> _slotData = new();
    private readonly HashSet<LootId> _reportedMissingDefinitions = new();
    private IReadOnlyList<LootEntry> _occupiedEntries;

    public IReadOnlyList<LootEntry> OccupiedEntries => _occupiedEntries;
    public IReadOnlyList<RaidInventorySlotData> Slots => _slotData;

    /// <summary>Raid alone projects stable physical addresses; aggregate reads serve feedback/value only.</summary>
    public bool RefreshInventory(
        PlayerLootReceiver receiver, LootDefinitionCatalog catalog, RaidLootPanelView view,
        long? totalValue, RaidLootSlotInteractionMode interactionMode,
        LootId selectedLootId, int selectedInventorySlotIndex)
    {
        _slotData.Clear();
        _occupiedEntries = null;
        if (receiver == null || catalog == null || view == null ||
            !LootTransferRequest.IsValidInventorySlotAddress(0, receiver.Revision) ||
            !view.EnsureSlotCount(PlayerLootReceiver.PhysicalSlotCount) ||
            !receiver.TryGetLootContent(out IReadOnlyList<LootEntry> aggregate))
        {
            view?.ShowUnavailable();
            return false;
        }

        int revision = receiver.Revision;
        for (int index = 0; index < PlayerLootReceiver.PhysicalSlotCount; index++)
        {
            if (!receiver.TryGetInventorySlot(index, out LootInventorySlot slot))
            {
                _slotData.Clear();
                view.ShowUnavailable();
                return false;
            }
            if (slot.IsEmpty)
            {
                _slotData.Add(RaidInventorySlotData.EmptyInventorySlot(index, revision));
                continue;
            }
            if (!catalog.TryGetByIndex(slot.CatalogIndex, out LootDefinition definition))
            {
                _slotData.Clear();
                view.ShowUnavailable();
                return false;
            }
            _slotData.Add(RaidInventorySlotData.CreateInventorySlot(
                new LootEntry(definition.LootId, slot.Quantity), definition, view.PlaceholderIcon,
                index, revision, slot.Origin));
        }

        _occupiedEntries = aggregate;
        if (view.Present(_slotData, totalValue, false, interactionMode, selectedLootId, selectedInventorySlotIndex))
            return true;
        view.ShowUnavailable();
        return false;
    }

    public bool Refresh(
        IInventoryReadSource inventorySource,
        LootDefinitionCatalog catalog,
        RaidLootPanelView view,
        long? totalValue,
        bool showEmptyState,
        RaidLootSlotInteractionMode interactionMode,
        LootId selectedLootId,
        Object logContext)
    {
        if (inventorySource == null)
        {
            view?.ShowUnavailable();
            return false;
        }

        return RefreshCore(
            inventorySource.TryGetLootContent,
            inventorySource.SlotCapacity,
            catalog,
            view,
            totalValue,
            showEmptyState,
            interactionMode,
            selectedLootId,
            logContext);
    }

    public bool Refresh(
        ILootContentReader contentReader,
        ILootSlotCapacityReader capacityReader,
        LootDefinitionCatalog catalog,
        RaidLootPanelView view,
        long? totalValue,
        bool showEmptyState,
        bool interactive,
        LootId selectedLootId,
        Object logContext)
    {
        return Refresh(
            contentReader,
            capacityReader,
            catalog,
            view,
            totalValue,
            showEmptyState,
            interactive
                ? RaidLootSlotInteractionMode.Transfer
                : RaidLootSlotInteractionMode.ReadOnly,
            selectedLootId,
            logContext);
    }

    public bool Refresh(
        ILootContentReader contentReader,
        ILootSlotCapacityReader capacityReader,
        LootDefinitionCatalog catalog,
        RaidLootPanelView view,
        long? totalValue,
        bool showEmptyState,
        RaidLootSlotInteractionMode interactionMode,
        LootId selectedLootId,
        Object logContext)
    {
        if (contentReader == null || capacityReader == null)
        {
            view?.ShowUnavailable();
            return false;
        }

        return RefreshCore(
            contentReader.TryGetLootContent,
            capacityReader.SlotCapacity,
            catalog,
            view,
            totalValue,
            showEmptyState,
            interactionMode,
            selectedLootId,
            logContext);
    }

    private delegate bool TryReadContent(out IReadOnlyList<LootEntry> content);

    private bool RefreshCore(
        TryReadContent tryReadContent,
        int slotCapacity,
        LootDefinitionCatalog catalog,
        RaidLootPanelView view,
        long? totalValue,
        bool showEmptyState,
        RaidLootSlotInteractionMode interactionMode,
        LootId selectedLootId,
        Object logContext)
    {
        if (tryReadContent == null || catalog == null || view == null ||
            !view.EnsureSlotCount(slotCapacity) ||
            !tryReadContent(out IReadOnlyList<LootEntry> content) ||
            !RaidInventoryProjection.TryBuild(content, slotCapacity, _projectedEntries))
        {
            _occupiedEntries = null;
            view?.ShowUnavailable();
            return false;
        }

        _occupiedEntries = content;
        _slotData.Clear();
        for (int i = 0; i < _projectedEntries.Count; i++)
        {
            LootEntry entry = _projectedEntries[i];
            if (!entry.IsValid)
            {
                _slotData.Add(RaidInventorySlotData.Empty);
                continue;
            }

            LootDefinition definition = null;
            if (!catalog.TryGet(entry.LootId.Value, out definition) && _reportedMissingDefinitions.Add(entry.LootId))
            {
                Debug.LogError($"{nameof(RaidLootPanelPresenter)} could not resolve metadata for loot '{entry.LootId.Value}'.", logContext);
            }

            _slotData.Add(RaidInventorySlotData.Create(entry, definition, view.PlaceholderIcon));
        }

        bool showEmpty = showEmptyState && content.Count == 0;
        if (!view.Present(_slotData, totalValue, showEmpty, interactionMode, selectedLootId))
        {
            view.ShowUnavailable();
            return false;
        }

        return true;
    }

    public void Clear()
    {
        _occupiedEntries = null;
        _projectedEntries.Clear();
        _slotData.Clear();
        _reportedMissingDefinitions.Clear();
    }
}
