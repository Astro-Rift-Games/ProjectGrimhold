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
