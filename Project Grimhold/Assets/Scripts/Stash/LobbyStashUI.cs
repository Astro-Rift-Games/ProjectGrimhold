using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The View component of the MVP pattern for the Stash and Loadout screen.
/// It reuses the same <see cref="RaidLootPanelView"/> panels and <see cref="RaidInventorySlotView"/>
/// slots as the Raid inventory. Every panel, slot and Equipment view is authored in the prefab:
/// this view only binds and drives them and never creates UI at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class LobbyStashUI : MonoBehaviour
{
    private static readonly LootContextActionId MoveAllId = new("town.loadout.move-all");

    /// <summary>One contextual equip action per Equipment slot, in slot order.</summary>
    private static readonly LootContextActionId[] EquipActionIds =
    {
        new("town.equipment.equip-weapon-slot-1"),
        new("town.equipment.equip-weapon-slot-2"),
        new("town.equipment.equip-helmet"),
        new("town.equipment.equip-armor"),
        new("town.equipment.equip-gloves"),
        new("town.equipment.equip-boots")
    };

    [SerializeField] private RaidLootPanelView _stashPanel;
    [SerializeField] private RaidLootPanelView _loadoutPanel;

    [SerializeField] private Button _takeAllButton;
    [SerializeField] private Button _leaveAllButton;

    [Header("Equipment slots (authored in the prefab, never created at runtime)")]
    [SerializeField] private RaidInventorySlotView _weaponSlot1View;
    [SerializeField] private RaidInventorySlotView _weaponSlot2View;
    [SerializeField] private RaidInventorySlotView _helmetView;
    [SerializeField] private RaidInventorySlotView _armorView;
    [SerializeField] private RaidInventorySlotView _glovesView;
    [SerializeField] private RaidInventorySlotView _bootsView;

    [SerializeField] private RaidLootContextMenuView _contextMenu;

    private readonly List<RaidInventorySlotData> _stashProjection = new();
    private readonly List<RaidInventorySlotData> _loadoutProjection = new();
    private readonly List<LootContextActionDescriptor> _contextActions = new();
    private RaidInventorySlotView[] _equipmentSlotViews;
    private LootId _contextLootId;
    private bool _hasReportedStashOverflow;
    private bool _hasReportedLoadoutOverflow;

    public event Action<LootId, bool, LootTransferQuantityMode> TransferRequested; // LootId, isFromStash, quantityMode
    public event Action TakeAllRequested;
    public event Action LeaveAllRequested;

    /// <summary>Local intention to occupy one Equipment slot with an owned unit.</summary>
    public event Action<LootId, EquipmentSlot> PreparedEquipmentAssignmentRequested;

    /// <summary>Local intention to release one Equipment slot.</summary>
    public event Action<EquipmentSlot> PreparedEquipmentClearRequested;

    /// <summary>The shared fallback icon authored on the stash panel, for unresolved definitions.</summary>
    public Sprite PlaceholderIcon => _stashPanel != null ? _stashPanel.PlaceholderIcon : null;

    private void Awake()
    {
        if (_takeAllButton != null) _takeAllButton.onClick.AddListener(OnTakeAllClicked);
        if (_leaveAllButton != null) _leaveAllButton.onClick.AddListener(OnLeaveAllClicked);
        if (_contextMenu != null) _contextMenu.ActionRequested += OnContextActionRequested;

        if (_stashPanel != null)
        {
            _stashPanel.SelectionRequested += OnStashSelectionRequested;
            _stashPanel.ContextRequested += OnPanelContextRequested;
        }
        else
        {
            ReportMissingView(nameof(_stashPanel));
        }

        if (_loadoutPanel != null)
        {
            _loadoutPanel.SelectionRequested += OnLoadoutSelectionRequested;
            _loadoutPanel.ContextRequested += OnPanelContextRequested;
        }
        else
        {
            ReportMissingView(nameof(_loadoutPanel));
        }

        BindEquipmentSlotViews();
    }

    private void OnDestroy()
    {
        if (_takeAllButton != null) _takeAllButton.onClick.RemoveListener(OnTakeAllClicked);
        if (_leaveAllButton != null) _leaveAllButton.onClick.RemoveListener(OnLeaveAllClicked);
        if (_contextMenu != null) _contextMenu.ActionRequested -= OnContextActionRequested;

        if (_stashPanel != null)
        {
            _stashPanel.SelectionRequested -= OnStashSelectionRequested;
            _stashPanel.ContextRequested -= OnPanelContextRequested;
        }

        if (_loadoutPanel != null)
        {
            _loadoutPanel.SelectionRequested -= OnLoadoutSelectionRequested;
            _loadoutPanel.ContextRequested -= OnPanelContextRequested;
        }

        if (_equipmentSlotViews == null)
        {
            return;
        }

        for (int index = 0; index < _equipmentSlotViews.Length; index++)
        {
            if (_equipmentSlotViews[index] != null)
            {
                _equipmentSlotViews[index].SelectionRequested -= OnEquipmentSlotSelected;
            }
        }
    }

    public void DisplayStash(IReadOnlyList<RaidInventorySlotData> items) =>
        PresentPanel(
            _stashPanel,
            items,
            _stashProjection,
            RaidLootSlotInteractionMode.TransferWithContextMenu,
            ref _hasReportedStashOverflow);

    public void DisplayLoadout(IReadOnlyList<RaidInventorySlotData> items) =>
        PresentPanel(
            _loadoutPanel,
            items,
            _loadoutProjection,
            RaidLootSlotInteractionMode.TransferWithContextMenu,
            ref _hasReportedLoadoutOverflow);

    /// <summary>
    /// Projects the six Equipment slots. Every occupied slot offers the release intention; only the
    /// weapon slots may become the effective weapon, which the Town does not preview.
    /// </summary>
    public void DisplayPreparedEquipment(IReadOnlyList<RaidInventorySlotData> slotData)
    {
        if (slotData == null || _equipmentSlotViews == null)
        {
            return;
        }

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        int count = Mathf.Min(slots.Length, slotData.Count);
        for (int index = 0; index < count; index++)
        {
            RaidInventorySlotView view = _equipmentSlotViews[index];
            if (view == null)
            {
                continue;
            }

            RaidInventorySlotData data = slotData[index];
            view.PresentEquipmentSlot(slots[index], in data, false, data.IsOccupied);
        }
    }

    /// <summary>
    /// Shows the authored pool of <paramref name="panel"/> completely, filling it with the received
    /// stacks in order and empty slots afterwards. Content beyond the authored pool cannot be shown,
    /// so it is reported once and the panel signals that it is full.
    /// </summary>
    private void PresentPanel(
        RaidLootPanelView panel,
        IReadOnlyList<RaidInventorySlotData> items,
        List<RaidInventorySlotData> projection,
        RaidLootSlotInteractionMode interactionMode,
        ref bool hasReportedOverflow)
    {
        if (panel == null)
        {
            return;
        }

        int capacity = panel.AuthoredSlotCount;
        if (!panel.EnsureSlotCount(capacity))
        {
            return;
        }

        int received = items?.Count ?? 0;
        bool overflows = received > capacity;
        if (overflows && !hasReportedOverflow)
        {
            hasReportedOverflow = true;
            Debug.LogError(
                $"{panel.name} holds {received} stacks but only {capacity} slots are authored in the prefab.",
                panel);
        }

        int visible = overflows ? capacity : received;
        projection.Clear();
        for (int index = 0; index < visible; index++)
        {
            projection.Add(items[index]);
        }

        while (projection.Count < capacity)
        {
            projection.Add(RaidInventorySlotData.Empty);
        }

        panel.Present(projection, null, visible == 0, interactionMode, default);
        if (overflows)
        {
            panel.ShowCapacityRejection();
        }
    }

    /// <summary>
    /// Binds the serialized Equipment views once. Nothing is instantiated: the panel and its six
    /// slots are authored in the prefab so the layout stays fully editable in the Inspector.
    /// </summary>
    private void BindEquipmentSlotViews()
    {
        var views = new[]
        {
            _weaponSlot1View, _weaponSlot2View, _helmetView,
            _armorView, _glovesView, _bootsView
        };

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        if (views.Length != slots.Length)
        {
            Debug.LogError(
                $"{nameof(LobbyStashUI)} exposes {views.Length} equipment views for {slots.Length} slots.",
                this);
            return;
        }

        for (int index = 0; index < views.Length; index++)
        {
            if (views[index] == null)
            {
                ReportMissingView($"{slots[index]} view");
                continue;
            }

            views[index].SelectionRequested += OnEquipmentSlotSelected;
        }

        _equipmentSlotViews = views;
    }

    /// <summary>Releases the Equipment slot whose authored view emitted the intention.</summary>
    private void OnEquipmentSlotSelected(LootId lootId, LootTransferQuantityMode mode)
    {
        if (_equipmentSlotViews == null)
        {
            return;
        }

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        for (int index = 0; index < _equipmentSlotViews.Length; index++)
        {
            RaidInventorySlotView view = _equipmentSlotViews[index];
            if (view != null && view.LootId == lootId && view.IsOccupied)
            {
                PreparedEquipmentClearRequested?.Invoke(slots[index]);
                return;
            }
        }
    }

    private void OnTakeAllClicked() => TakeAllRequested?.Invoke();

    private void OnLeaveAllClicked() => LeaveAllRequested?.Invoke();

    private void OnStashSelectionRequested(LootId lootId, LootTransferQuantityMode mode) =>
        TransferRequested?.Invoke(lootId, true, mode);

    private void OnLoadoutSelectionRequested(LootId lootId, LootTransferQuantityMode mode) =>
        TransferRequested?.Invoke(lootId, false, mode);

    /// <summary>
    /// Opens the contextual menu for one owned stack. Both panels offer the same equip intentions,
    /// so a unit can be equipped from the Stash or from the Loadout; only the Loadout offers the
    /// bulk move back to the Stash.
    /// </summary>
    private void OnPanelContextRequested(LootId lootId, Vector2 screenPosition)
    {
        if (_contextMenu == null || !lootId.IsValid)
        {
            return;
        }

        _contextLootId = lootId;
        _contextActions.Clear();
        _contextActions.Add(new LootContextActionDescriptor(MoveAllId, "Mover todo al Stash", true, null));

        LootCategory category = ResolveCategory(lootId);
        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        for (int index = 0; index < slots.Length; index++)
        {
            if (!EquipmentSlotRules.IsCompatible(category, slots[index]))
            {
                continue;
            }

            _contextActions.Add(new LootContextActionDescriptor(
                EquipActionIds[index],
                $"Equipar en {ResolveSlotLabel(slots[index])}",
                true,
                null));
        }

        _contextMenu.Show(_contextActions, screenPosition);
    }

    private void OnContextActionRequested(LootContextActionId actionId)
    {
        _contextMenu?.Hide();
        if (!_contextLootId.IsValid) return;
        if (actionId == MoveAllId)
        {
            TransferRequested?.Invoke(_contextLootId, false, LootTransferQuantityMode.FullStack);
            _contextLootId = default;
            return;
        }

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        for (int index = 0; index < EquipActionIds.Length && index < slots.Length; index++)
        {
            if (actionId == EquipActionIds[index])
            {
                PreparedEquipmentAssignmentRequested?.Invoke(_contextLootId, slots[index]);
                break;
            }
        }

        _contextLootId = default;
    }

    /// <summary>
    /// Reads the catalog classification already projected into the visible panels, so the menu can
    /// offer only the slots that may receive this unit without depending on the catalog itself.
    /// </summary>
    private LootCategory ResolveCategory(LootId lootId)
    {
        for (int index = 0; index < _loadoutProjection.Count; index++)
        {
            if (_loadoutProjection[index].IsOccupied && _loadoutProjection[index].LootId == lootId)
            {
                return _loadoutProjection[index].Category;
            }
        }

        for (int index = 0; index < _stashProjection.Count; index++)
        {
            if (_stashProjection[index].IsOccupied && _stashProjection[index].LootId == lootId)
            {
                return _stashProjection[index].Category;
            }
        }

        return LootCategory.None;
    }

    private static string ResolveSlotLabel(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.WeaponSlot1 => "Weapon Slot 1",
        EquipmentSlot.WeaponSlot2 => "Weapon Slot 2",
        EquipmentSlot.Helmet => "Casco",
        EquipmentSlot.Armor => "Armadura",
        EquipmentSlot.Gloves => "Guantes",
        EquipmentSlot.Boots => "Botas",
        _ => "Equipment"
    };

    private void ReportMissingView(string fieldName) =>
        Debug.LogError(
            $"{nameof(LobbyStashUI)} has no serialized {fieldName}. Assign it on the stash prefab.",
            this);
}
