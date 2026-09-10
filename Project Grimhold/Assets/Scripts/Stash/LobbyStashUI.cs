using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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
        new("town.equipment.equip-boots"),
        new("town.equipment.equip-set-a-off-hand"),
        new("town.equipment.equip-set-b-off-hand")
    };

    [SerializeField] private RaidLootPanelView _stashPanel;
    [SerializeField] private RaidLootPanelView _loadoutPanel;

    [SerializeField] private Button _takeAllButton;
    [SerializeField] private Button _leaveAllButton;

    [Header("Equipment slots (authored in the prefab, never created at runtime)")]
    [FormerlySerializedAs("_weaponSlot1View"), SerializeField] private RaidInventorySlotView _weaponSetAMainHandView;
    [FormerlySerializedAs("_weaponSlot2View"), SerializeField] private RaidInventorySlotView _weaponSetBMainHandView;
    [SerializeField] private RaidInventorySlotView _helmetView;
    [SerializeField] private RaidInventorySlotView _armorView;
    [SerializeField] private RaidInventorySlotView _glovesView;
    [SerializeField] private RaidInventorySlotView _bootsView;
    [SerializeField] private RaidInventorySlotView _weaponSetAOffHandView;
    [SerializeField] private RaidInventorySlotView _weaponSetBOffHandView;

    [SerializeField] private RaidLootContextMenuView _contextMenu;
    [SerializeField] private EquipmentTooltipView _tooltipView;

    private readonly List<RaidInventorySlotData> _stashProjection = new();
    private readonly List<RaidInventorySlotData> _loadoutProjection = new();
    private readonly List<LootContextActionDescriptor> _contextActions = new();
    private RaidInventorySlotView[] _equipmentSlotViews;
    private LootId _contextLootId;
    private bool _contextIsFromStash;
    private bool _hasReportedStashOverflow;
    private bool _hasReportedLoadoutOverflow;
    private bool _setAOffHandBlocked;
    private bool _setBOffHandBlocked;

    public event Action<LootId, bool, LootTransferQuantityMode> TransferRequested; // LootId, isFromStash, quantityMode
    public event Action TakeAllRequested;
    public event Action LeaveAllRequested;

    /// <summary>Local intention to occupy one Equipment slot with an owned unit.</summary>
    public event Action<LootId, EquipmentSlot> PreparedEquipmentAssignmentRequested;

    /// <summary>Local intention to release one Equipment slot.</summary>
    public event Action<EquipmentSlot> PreparedEquipmentClearRequested;

    /// <summary>The shared fallback icon authored on the stash panel, for unresolved definitions.</summary>
    public Sprite PlaceholderIcon => _stashPanel != null ? _stashPanel.PlaceholderIcon : null;
    public EquipmentTooltipView TooltipView => _tooltipView;

    private void Awake()
    {
        if (_takeAllButton != null) _takeAllButton.onClick.AddListener(OnTakeAllClicked);
        if (_leaveAllButton != null) _leaveAllButton.onClick.AddListener(OnLeaveAllClicked);
        if (_contextMenu != null) _contextMenu.ActionRequested += OnContextActionRequested;

        if (_stashPanel != null)
        {
            _stashPanel.SelectionRequested += OnStashSelectionRequested;
            _stashPanel.ContextRequested += OnStashContextRequested;
            BindPanelTooltipEvents(_stashPanel);
        }
        else
        {
            ReportMissingView(nameof(_stashPanel));
        }

        if (_loadoutPanel != null)
        {
            _loadoutPanel.SelectionRequested += OnLoadoutSelectionRequested;
            _loadoutPanel.ContextRequested += OnLoadoutContextRequested;
            BindPanelTooltipEvents(_loadoutPanel);
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
            _stashPanel.ContextRequested -= OnStashContextRequested;
            UnbindPanelTooltipEvents(_stashPanel);
        }

        if (_loadoutPanel != null)
        {
            _loadoutPanel.SelectionRequested -= OnLoadoutSelectionRequested;
            _loadoutPanel.ContextRequested -= OnLoadoutContextRequested;
            UnbindPanelTooltipEvents(_loadoutPanel);
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
                UnbindEquipmentTooltipEvents(_equipmentSlotViews[index]);
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
    /// Projects the eight Equipment slots. Every occupied slot offers the release intention; only the
    /// Main Hand slots may become the effective weapon, which the Town does not preview.
    /// </summary>
    public void DisplayPreparedEquipment(
        IReadOnlyList<RaidInventorySlotData> slotData,
        bool setAOffHandBlocked = false,
        bool setBOffHandBlocked = false)
    {
        if (slotData == null || _equipmentSlotViews == null)
        {
            return;
        }

        _setAOffHandBlocked = setAOffHandBlocked;
        _setBOffHandBlocked = setBOffHandBlocked;

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
            EquipmentSlot slot = slots[index];
            bool blocked = slot == EquipmentSlot.WeaponSetAOffHand && setAOffHandBlocked ||
                           slot == EquipmentSlot.WeaponSetBOffHand && setBOffHandBlocked;
            view.PresentEquipmentSlot(slot, in data, false, data.IsOccupied, blocked);
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
    /// Binds the serialized Equipment views once. Nothing is instantiated: the panel and its eight
    /// slots are authored in the prefab so the layout stays fully editable in the Inspector.
    /// </summary>
    private void BindEquipmentSlotViews()
    {
        var views = new[]
        {
            _weaponSetAMainHandView, _weaponSetBMainHandView, _helmetView,
            _armorView, _glovesView, _bootsView,
            _weaponSetAOffHandView, _weaponSetBOffHandView
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
            BindEquipmentTooltipEvents(views[index]);
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
    /// Opens the contextual menu for one owned stack. Both panels offer the inverse bulk transfer
    /// and the same equip intentions, so a unit can be equipped from the Stash or from the Loadout.
    /// </summary>
    private void OnStashContextRequested(LootId lootId, RectTransform anchor) =>
        OpenPanelContext(lootId, anchor, true);

    private void OnLoadoutContextRequested(LootId lootId, RectTransform anchor) =>
        OpenPanelContext(lootId, anchor, false);

    private void OpenPanelContext(LootId lootId, RectTransform anchor, bool isFromStash)
    {
        _tooltipView?.Hide();
        if (_contextMenu == null || !lootId.IsValid)
        {
            return;
        }

        _contextLootId = lootId;
        _contextIsFromStash = isFromStash;
        _contextActions.Clear();
        string transferLabel = isFromStash
            ? "Mover todo al Inventario"
            : "Mover todo al Stash";
        _contextActions.Add(new LootContextActionDescriptor(MoveAllId, transferLabel, true, null));

        LootCategory category = ResolveCategory(lootId);
        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        for (int index = 0; index < slots.Length; index++)
        {
            if (!EquipmentSlotRules.IsCompatible(category, slots[index]) ||
                category == LootCategory.Weapon &&
                !IsWeaponCompatibleWithSlot(lootId, slots[index]))
            {
                continue;
            }

            bool enabled = slots[index] != EquipmentSlot.WeaponSetAOffHand || !_setAOffHandBlocked;
            enabled &= slots[index] != EquipmentSlot.WeaponSetBOffHand || !_setBOffHandBlocked;
            _contextActions.Add(new LootContextActionDescriptor(
                EquipActionIds[index],
                $"Equipar en {ResolveSlotLabel(slots[index])}",
                enabled,
                null));
        }

        _contextMenu.Show(_contextActions, anchor);
    }

    private void OnDisable() => _tooltipView?.Hide();

    private void BindPanelTooltipEvents(RaidLootPanelView panel)
    {
        panel.TooltipRequested += OnTooltipRequested;
        panel.TooltipDismissRequested += OnTooltipDismissRequested;
    }

    private void UnbindPanelTooltipEvents(RaidLootPanelView panel)
    {
        panel.TooltipRequested -= OnTooltipRequested;
        panel.TooltipDismissRequested -= OnTooltipDismissRequested;
    }

    private void BindEquipmentTooltipEvents(RaidInventorySlotView view)
    {
        view.TooltipRequested += OnTooltipRequested;
        view.TooltipDismissRequested += OnTooltipDismissRequested;
    }

    private void UnbindEquipmentTooltipEvents(RaidInventorySlotView view)
    {
        view.TooltipRequested -= OnTooltipRequested;
        view.TooltipDismissRequested -= OnTooltipDismissRequested;
    }

    private void OnTooltipRequested(
        EquipmentTooltipPresentation presentation,
        RectTransform anchor) =>
        _tooltipView?.Show(in presentation, anchor);

    private void OnTooltipDismissRequested(RectTransform anchor)
    {
        if (_tooltipView != null && _tooltipView.CurrentAnchor == anchor)
        {
            _tooltipView.Hide();
        }
    }

    private void OnContextActionRequested(LootContextActionId actionId)
    {
        _contextMenu?.Hide();
        if (!_contextLootId.IsValid) return;
        if (actionId == MoveAllId)
        {
            TransferRequested?.Invoke(
                _contextLootId,
                _contextIsFromStash,
                LootTransferQuantityMode.FullStack);
            _contextLootId = default;
            _contextIsFromStash = false;
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
        _contextIsFromStash = false;
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
        EquipmentSlot.WeaponSetAMainHand => "Set A / Main Hand",
        EquipmentSlot.WeaponSetBMainHand => "Set B / Main Hand",
        EquipmentSlot.WeaponSetAOffHand => "Set A / Off Hand",
        EquipmentSlot.WeaponSetBOffHand => "Set B / Off Hand",
        EquipmentSlot.Helmet => "Casco",
        EquipmentSlot.Armor => "Armadura",
        EquipmentSlot.Gloves => "Guantes",
        EquipmentSlot.Boots => "Botas",
        _ => "Equipment"
    };

    private bool IsWeaponCompatibleWithSlot(LootId lootId, EquipmentSlot slot)
    {
        for (int index = 0; index < _loadoutProjection.Count; index++)
        {
            RaidInventorySlotData data = _loadoutProjection[index];
            if (data.IsOccupied && data.LootId == lootId)
            {
                return data.HasWeaponDefinition &&
                    (data.WeaponHandedness == WeaponHandedness.OneHanded || EquipmentSlotRules.IsMainHandSlot(slot));
            }
        }

        for (int index = 0; index < _stashProjection.Count; index++)
        {
            RaidInventorySlotData data = _stashProjection[index];
            if (data.IsOccupied && data.LootId == lootId)
            {
                return data.HasWeaponDefinition &&
                    (data.WeaponHandedness == WeaponHandedness.OneHanded || EquipmentSlotRules.IsMainHandSlot(slot));
            }
        }

        return false;
    }

    private void ReportMissingView(string fieldName) =>
        Debug.LogError(
            $"{nameof(LobbyStashUI)} has no serialized {fieldName}. Assign it on the stash prefab.",
            this);
}
