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
        LootId selectedLootId, int selectedInventorySlotIndex,
        CharacterAttributeState? attributes = null)
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
            receiver.TryGetInventorySlotModifiers(index, out WeaponInstanceModifiersNetworkData modifiers);
            _slotData.Add(RaidInventorySlotData.CreateInventorySlot(
                new LootEntry(definition.LootId, slot.Quantity), definition, view.PlaceholderIcon,
                index, revision, slot.Origin, modifiers, attributes, catalog));
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

    /// <summary>
    /// Raid container cells: stacks keep one aggregate cell, and every weapon unit has its own cell
    /// addressed by unit position, with its own tooltip (ItemInstanceArchitecture section 6.3).
    /// </summary>
    public bool RefreshContainer(
        NetworkLootContainer container, LootDefinitionCatalog catalog, RaidLootPanelView view,
        bool showEmptyState, RaidLootSlotInteractionMode interactionMode, LootId selectedLootId,
        CharacterAttributeState? attributes, Object logContext)
    {
        _slotData.Clear();
        _occupiedEntries = null;
        if (container == null || catalog == null || view == null ||
            !view.EnsureSlotCount(container.SlotCapacity) ||
            !container.TryGetLootContent(out IReadOnlyList<LootEntry> content))
        {
            view?.ShowUnavailable();
            return false;
        }

        for (int i = 0; i < content.Count; i++)
        {
            LootEntry entry = content[i];
            if (!catalog.TryGet(entry.LootId.Value, out LootDefinition definition) &&
                _reportedMissingDefinitions.Add(entry.LootId))
            {
                Debug.LogError($"{nameof(RaidLootPanelPresenter)} could not resolve metadata for loot '{entry.LootId.Value}'.", logContext);
            }

            if (definition == null || definition.Category != LootCategory.Weapon ||
                !catalog.TryGetIndex(entry.LootId, out int catalogIndex))
            {
                _slotData.Add(RaidInventorySlotData.Create(entry, definition, view.PlaceholderIcon));
                continue;
            }

            int units = 0;
            for (int unitIndex = 0; unitIndex < container.WeaponUnitCapacity; unitIndex++)
            {
                if (!container.TryGetWeaponUnit(unitIndex, out ContainerWeaponUnit unit) || unit.IsEmpty ||
                    unit.CatalogIndex != catalogIndex) continue;
                _slotData.Add(RaidInventorySlotData.CreateContainerUnit(definition, view.PlaceholderIcon,
                    unitIndex, unit.Modifiers, attributes, catalog));
                units++;
            }

            if (units != entry.Amount)
            {
                _slotData.Clear();
                view.ShowUnavailable();
                return false;
            }
        }

        if (_slotData.Count > container.SlotCapacity)
        {
            _slotData.Clear();
            view.ShowUnavailable();
            return false;
        }

        while (_slotData.Count < container.SlotCapacity) _slotData.Add(RaidInventorySlotData.Empty);
        _occupiedEntries = content;
        if (!view.Present(_slotData, null, showEmptyState && content.Count == 0, interactionMode, selectedLootId))
        {
            view.ShowUnavailable();
            return false;
        }

        return true;
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

            // Town keeps physical entries (including duplicate templates) aligned with this projection.
            // Raid/container reads have no Town instance or attributes and retain definition-only tooltips.
            TownInventoryProjection town = content as TownInventoryProjection;
            EquipmentInstance instance = town != null && i < town.PhysicalItems.Count
                ? town.PhysicalItems[i].Instance : null;
            _slotData.Add(RaidInventorySlotData.Create(entry, definition, view.PlaceholderIcon,
                instance, town?.ConfirmedAttributes, catalog));
        }

        if (content is TownInventoryProjection physicalTown)
        {
            // Town positions are persistent addresses, not indices into an occupied-only snapshot.
            _slotData.Clear();
            for (int i = 0; i < slotCapacity; i++) _slotData.Add(RaidInventorySlotData.Empty);
            foreach (var item in physicalTown.PhysicalItems)
            {
                if (item.Slot < 0 || item.Slot >= slotCapacity)
                {
                    view.ShowUnavailable();
                    return false;
                }
                catalog.TryGet(item.LootId.Value, out var definition);
                _slotData[item.Slot] = RaidInventorySlotData.Create(new LootEntry(item.LootId, item.Amount),
                    definition, view.PlaceholderIcon, item.Instance, physicalTown.ConfirmedAttributes, catalog);
            }
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
