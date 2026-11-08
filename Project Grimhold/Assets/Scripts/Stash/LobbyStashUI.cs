using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;

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
    private static readonly LootContextActionId SplitId = new("town.inventory.split");

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

    [SerializeField] private UnityEngine.UI.Button _takeAllButton;
    [SerializeField] private UnityEngine.UI.Button _leaveAllButton;
    [SerializeField] private UnityEngine.UI.Button _stashSortButton;
    [SerializeField] private UnityEngine.UI.Button _loadoutSortButton;
    [SerializeField] private UnityEngine.UI.Button _stashPreviousPage;
    [SerializeField] private UnityEngine.UI.Button _stashNextPage;
    [SerializeField] private TMP_Text _stashPageLabel;
    private readonly List<RaidInventorySlotData> _stashItems = new();
    private int _stashCapacity = LocalProfileStore.DefaultStashCapacity;
    private int _stashPage;

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
    [SerializeField] private DragPreviewView _dragPreview;
    [SerializeField] private GemInstallConfirmationView _gemInstallView;
    [SerializeField] private QuantitySelectorDialogView _quantityDialogPrefab;
    [SerializeField] private RectTransform _quantityDialogParent;
    private QuantitySelectorDialogView _quantityDialog;
    private Func<InventoryList, int, LootId, int, int, int> _splitDestination;
    private bool _actionsInteractable;
    private InventoryList _splitList;
    private int _splitSlot = -1;
    private LootId _splitLootId;
    private int _splitQuantity;

    public event Action<InventoryList, int, int, int, LootId> SplitRequested;

    /// <summary>The presenter supplies current-state split validation; the view owns only modal state.</summary>
    public void BindSplit(Func<InventoryList, int, LootId, int, int, int> destination)
    {
        CloseQuantityDialog();
        _splitDestination = destination;
    }

    private QuantitySelectorDialogView QuantityDialog
    {
        get
        {
            if (_quantityDialog == null && _quantityDialogPrefab != null && _quantityDialogParent != null)
            {
                _quantityDialog = Instantiate(_quantityDialogPrefab, _quantityDialogParent, false);
                _quantityDialog.ConfirmRequested += ConfirmSplit;
                _quantityDialog.CancelRequested += CloseQuantityDialog;
            }
            return _quantityDialog;
        }
    }

    private void CloseQuantityDialog()
    {
        if (_quantityDialog != null) _quantityDialog.Close();
        _splitSlot = -1;
    }

    private void ConfirmSplit(int amount)
    {
        int destination = _actionsInteractable && !_gemLocksMutations && _splitSlot >= 0
            ? _splitDestination?.Invoke(_splitList, _splitSlot, _splitLootId, _splitQuantity, amount) ?? -1 : -1;
        var list = _splitList;
        int source = _splitSlot;
        var id = _splitLootId;
        CloseQuantityDialog();
        if (destination >= 0) SplitRequested?.Invoke(list, source, amount, destination, id);
    }

    private RaidInventorySlotData ContextData => _contextIsFromStash
        ? (_contextSlot >= 0 && _contextSlot < _stashItems.Count ? _stashItems[_contextSlot] : default)
        : (_contextSlot >= 0 && _contextSlot < _loadoutProjection.Count ? _loadoutProjection[_contextSlot] : default);

    private bool CanSplitContext() => _actionsInteractable && !_gemLocksMutations &&
        QuantitySelectorRange.TryCreate(ContextData.Amount, out _) &&
        (_splitDestination?.Invoke(_contextIsFromStash ? InventoryList.Stash : InventoryList.Loadout,
            _contextSlot, _contextLootId, ContextData.Amount, 1) ?? -1) >= 0;
    [SerializeField] private TMP_Text _gemFeedbackText;
    private float _gemFeedbackRemaining;
    public GemInstallConfirmationView GemInstallView => _gemInstallView;
    public RaidLootPanelView StashPanel => _stashPanel;
    public RaidLootPanelView LoadoutPanel => _loadoutPanel;
    public int StashPageStart => _stashPanel != null ? _stashPage * _stashPanel.AuthoredSlotCount : 0;
    public event Action<InventoryList, int, LootId, List<LootContextActionDescriptor>> ContextActionsRequested;
    public event Action StashPageChanged;
    private bool _gemLocksMutations;

    public void PresentGemFeedback(string message, bool persistent)
    {
        if (_gemFeedbackText == null) return;
        _gemFeedbackText.text = message ?? string.Empty;
        _gemFeedbackText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        _gemFeedbackRemaining = persistent ? 0f : 1.5f;
    }

    private void Update()
    {
        if (_gemFeedbackRemaining <= 0f) return;
        _gemFeedbackRemaining -= Time.unscaledDeltaTime;
        if (_gemFeedbackRemaining <= 0f && _gemFeedbackText != null) _gemFeedbackText.gameObject.SetActive(false);
    }

    public void SetGemLocksMutations(bool locked)
    {
        _gemLocksMutations = locked;
        if (locked) CloseQuantityDialog();
        if (locked) { _contextMenu?.Hide(); _tooltipView?.Hide(); }
        if (_equipmentSlotViews != null) foreach (var cell in _equipmentSlotViews)
            if (cell != null && locked) cell.SetInteraction(false, false);
    }

    private readonly List<RaidInventorySlotData> _stashProjection = new();
    private readonly List<RaidInventorySlotData> _loadoutProjection = new();
    private readonly List<LootContextActionDescriptor> _contextActions = new();
    private RaidInventorySlotView[] _equipmentSlotViews;
    private LootId _contextLootId;
    private bool _contextIsFromStash;
    private int _contextSlot = -1;
    private RaidInventorySlotView[] _stashCells;
    private RaidInventorySlotView[] _loadoutCells;
    private readonly List<Action> _unbindCells = new();
    private bool _hasReportedLoadoutOverflow;
    private bool _setAOffHandBlocked;
    private bool _setBOffHandBlocked;

    public event Action<LootId, bool, LootTransferQuantityMode> TransferRequested; // LootId, isFromStash, quantityMode
    /// <summary>Town-only addresses; unlike Raid metadata these also cover Stash and overflow slots.</summary>
    public event Action<int, LootId, bool, LootTransferQuantityMode> SlotTransferRequested;
    public event Action<int, DragPayload> SlotDragStarted;
    public event Action<InventoryList, int> SlotDropReceived;
    public event Action TakeAllRequested;
    public event Action LeaveAllRequested;
    public event Action<InventoryList> SortRequested;
    public event Action<LootContextActionDescriptor> GemContextActionRequested;

    public event Action<DragPayload> DragStarted;
    public event Action DragUpdated;
    public event Action<bool> DragEnded;
    public event Action<DragSlotLocation, EquipmentSlot> DropReceived;

    /// <summary>Local intention to occupy one Equipment slot with an owned unit.</summary>
    public event Action<LootId, EquipmentSlot, bool> PreparedEquipmentAssignmentRequested;

    /// <summary>Local intention to release one Equipment slot.</summary>
    public event Action<EquipmentSlot> PreparedEquipmentClearRequested;

    /// <summary>The shared fallback icon authored on the stash panel, for unresolved definitions.</summary>
    public Sprite PlaceholderIcon => _stashPanel != null ? _stashPanel.PlaceholderIcon : null;
    public EquipmentTooltipView TooltipView => _tooltipView;
    public DragPreviewView DragPreview => _dragPreview;

    private void Awake()
    {
        if (_stashPreviousPage != null) _stashPreviousPage.onClick.AddListener(OnPreviousStashPage);
        if (_stashNextPage != null) _stashNextPage.onClick.AddListener(OnNextStashPage);
        if (_takeAllButton != null) _takeAllButton.onClick.AddListener(OnTakeAllClicked);
        if (_leaveAllButton != null) _leaveAllButton.onClick.AddListener(OnLeaveAllClicked);
        if (_stashSortButton != null) _stashSortButton.onClick.AddListener(OnStashSortClicked);
        else ReportMissingView(nameof(_stashSortButton));
        if (_loadoutSortButton != null) _loadoutSortButton.onClick.AddListener(OnLoadoutSortClicked);
        else ReportMissingView(nameof(_loadoutSortButton));
        SetActionsInteractable(false);
        if (_contextMenu != null) _contextMenu.ActionRequested += OnContextActionRequested;

        if (_stashPanel != null)
        {
            _stashPanel.SetDragLocation(DragSlotLocation.Stash);
            _stashPanel.SelectionRequested += OnStashSelectionRequested;
            _stashPanel.ContextRequested += OnStashContextRequested;
            BindPanelTooltipEvents(_stashPanel);
            
            _stashPanel.DragUpdated += () => DragUpdated?.Invoke();
            _stashPanel.DragEnded += b => DragEnded?.Invoke(b);
            _stashPanel.DropReceived += (loc, targetSlot) => DropReceived?.Invoke(loc, targetSlot);
        }
        else
        {
            ReportMissingView(nameof(_stashPanel));
        }

        if (_loadoutPanel != null)
        {
            _loadoutPanel.SetDragLocation(DragSlotLocation.Inventory);
            _loadoutPanel.SelectionRequested += OnLoadoutSelectionRequested;
            _loadoutPanel.ContextRequested += OnLoadoutContextRequested;
            BindPanelTooltipEvents(_loadoutPanel);
            
            _loadoutPanel.DragUpdated += () => DragUpdated?.Invoke();
            _loadoutPanel.DragEnded += b => DragEnded?.Invoke(b);
            _loadoutPanel.DropReceived += (loc, targetSlot) => DropReceived?.Invoke(loc, targetSlot);
        }
        else
        {
            ReportMissingView(nameof(_loadoutPanel));
        }

        _stashCells = BindPanelCells(_stashPanel, InventoryList.Stash);
        _loadoutCells = BindPanelCells(_loadoutPanel, InventoryList.Loadout);
        BindEquipmentSlotViews();
    }

    private void OnDestroy()
    {
        if (_quantityDialog != null)
        {
            _quantityDialog.ConfirmRequested -= ConfirmSplit;
            _quantityDialog.CancelRequested -= CloseQuantityDialog;
            Destroy(_quantityDialog.gameObject);
        }
        foreach (Action unbind in _unbindCells) unbind();
        _unbindCells.Clear();
        if (_stashPreviousPage != null) _stashPreviousPage.onClick.RemoveListener(OnPreviousStashPage);
        if (_stashNextPage != null) _stashNextPage.onClick.RemoveListener(OnNextStashPage);
        if (_takeAllButton != null) _takeAllButton.onClick.RemoveListener(OnTakeAllClicked);
        if (_leaveAllButton != null) _leaveAllButton.onClick.RemoveListener(OnLeaveAllClicked);
        if (_stashSortButton != null) _stashSortButton.onClick.RemoveListener(OnStashSortClicked);
        if (_loadoutSortButton != null) _loadoutSortButton.onClick.RemoveListener(OnLoadoutSortClicked);
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

    public void DisplayStash(IReadOnlyList<RaidInventorySlotData> items, int capacity = LocalProfileStore.DefaultStashCapacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _stashCapacity = capacity;
        _stashItems.Clear();
        if (items != null) _stashItems.AddRange(items);
        PresentStashPage();
    }

    private int StashPageCount => _stashPanel == null || _stashPanel.AuthoredSlotCount == 0 ? 1 :
        1 + (Math.Max(_stashCapacity, _stashItems.Count) - 1) / _stashPanel.AuthoredSlotCount;

    private void OnPreviousStashPage() => ChangeStashPage(-1);
    private void OnNextStashPage() => ChangeStashPage(1);

    private void ChangeStashPage(int delta)
    {
        _stashPage = Mathf.Clamp(_stashPage + delta, 0, StashPageCount - 1);
        PresentStashPage();
        StashPageChanged?.Invoke();
    }

    private void DismissStashInteraction()
    {
        CloseQuantityDialog();
        _tooltipView?.Hide();
        _contextMenu?.Hide();
        _contextLootId = default;
        _contextSlot = -1;
        // Cancel the intention even when no drag-preview view is configured.
        DragEnded?.Invoke(false);
        if (_dragPreview != null && _dragPreview.IsActive)
        {
            _dragPreview.Hide();
        }
    }

    private void PresentStashPage()
    {
        DismissStashInteraction();
        if (_stashPanel == null || _stashPanel.AuthoredSlotCount == 0) return;
        int pageSize = _stashPanel.AuthoredSlotCount;
        _stashPage = Mathf.Clamp(_stashPage, 0, StashPageCount - 1);
        int start = _stashPage * pageSize;
        int count = Math.Min(pageSize, Math.Max(_stashCapacity, _stashItems.Count) - start);
        _stashProjection.Clear();
        for (int i = 0; i < count; i++)
            _stashProjection.Add(start + i < _stashItems.Count ? _stashItems[start + i] : RaidInventorySlotData.Empty);
        if (!_stashPanel.EnsureSlotCount(count)) return;
        _stashPanel.Present(_stashProjection, null, !HasOccupied(_stashItems),
            RaidLootSlotInteractionMode.TransferWithContextMenu, default);
        MarkOverflow(_stashCells, _stashProjection, start, _stashCapacity);
        if (_stashPreviousPage != null) _stashPreviousPage.interactable = _stashPage > 0;
        if (_stashNextPage != null) _stashNextPage.interactable = _stashPage < StashPageCount - 1;
        if (_stashPageLabel != null) _stashPageLabel.text = $"{_stashPage + 1} / {StashPageCount}";
    }

    /// <summary>Input index is the physical Town slot, including empty interior cells.</summary>
    public void DisplayLoadout(IReadOnlyList<RaidInventorySlotData> items)
    {
        DismissStashInteraction();
        PresentPanel(
            _loadoutPanel,
            items,
            _loadoutProjection,
            RaidLootSlotInteractionMode.TransferWithContextMenu,
            ref _hasReportedLoadoutOverflow);
        MarkOverflow(_loadoutCells, _loadoutProjection, 0, LocalProfileSnapshot.MaxLoadoutSlots);
    }

    public void RestoreOverflowHighlights()
    {
        MarkOverflow(_stashCells, _stashProjection, StashPageStart, _stashCapacity);
        MarkOverflow(_loadoutCells, _loadoutProjection, 0, LocalProfileSnapshot.MaxLoadoutSlots);
    }

    public bool IsEquipmentDropReserved(EquipmentSlot slot) =>
        slot == EquipmentSlot.WeaponSetAOffHand && _setAOffHandBlocked ||
        slot == EquipmentSlot.WeaponSetBOffHand && _setBOffHandBlocked;

    /// <summary>
    /// Projects prepared Equipment, including visual-only two-handed reservations.
    /// Town does not preview an active weapon Set, so Set visuals remain neutral.
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
            if (blocked)
            {
                EquipmentSlot mainHand = EquipmentSlotRules.GetMainHandSlot(EquipmentSlotRules.GetWeaponSet(slot));
                int mainIndex = System.Array.IndexOf(slots, mainHand);
                data = mainIndex >= 0 && mainIndex < slotData.Count ? slotData[mainIndex] : RaidInventorySlotData.Empty;
            }
            view.PresentEquipmentSlot(slot, in data, false, data.IsOccupied, blocked);
        }
    }

    /// <summary>
    /// Shows physical cells without compaction, padding the authored pool with empties. A physical
    /// extent beyond the pool is reported once: the Loadout has no authored paging controls.
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
                $"{panel.name} needs physical slots 0..{received - 1} but only {capacity} slots are authored in the prefab; Loadout paging is not authored.",
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

        panel.Present(projection, null, !HasOccupied(projection), interactionMode, default);
        if (overflows)
        {
            panel.ShowCapacityRejection();
        }
    }

    private static bool HasOccupied(IReadOnlyList<RaidInventorySlotData> items)
    {
        for (int i = 0; i < items.Count; i++) if (items[i].IsOccupied) return true;
        return false;
    }

    private static void MarkOverflow(
        RaidInventorySlotView[] cells, IReadOnlyList<RaidInventorySlotData> projection, int start, int capacity)
    {
        if (cells == null) return;
        for (int i = 0; i < cells.Length; i++)
            cells[i].SetDropHighlight(i < projection.Count && projection[i].IsOccupied && start + i >= capacity
                ? DropHighlightState.Invalid : DropHighlightState.None);
    }

    /// <summary>
    /// Cache only the existing panel-local cells. The prefab's hierarchy and authored pool use the
    /// same cell order; no new serialized references, cell objects or Raid addresses are invented.
    /// </summary>
    private RaidInventorySlotView[] BindPanelCells(RaidLootPanelView panel, InventoryList list)
    {
        if (panel == null) return Array.Empty<RaidInventorySlotView>();
        RaidInventorySlotView[] cells = panel.GetComponentsInChildren<RaidInventorySlotView>(true);
        if (cells.Length != panel.AuthoredSlotCount)
        {
            Debug.LogError($"{panel.name} must contain exactly its {panel.AuthoredSlotCount} authored cells.", panel);
            return Array.Empty<RaidInventorySlotView>();
        }
        for (int i = 0; i < cells.Length; i++)
        {
            int cellIndex = i;
            RaidInventorySlotView cell = cells[i];
            Action<DragPayload> drag = payload =>
            {
                DragStarted?.Invoke(payload);
                SlotDragStarted?.Invoke(PhysicalSlot(list, cellIndex), payload);
            };
            Action<DragSlotLocation, EquipmentSlot> drop = (_, __) =>
            {
                if (isActiveAndEnabled && cell.gameObject.activeInHierarchy && cellIndex < panel.SlotCount)
                    SlotDropReceived?.Invoke(list, PhysicalSlot(list, cellIndex));
            };
            cell.DragStarted += drag;
            cell.DropReceived += drop;
            _unbindCells.Add(() => { if (cell != null) { cell.DragStarted -= drag; cell.DropReceived -= drop; } });
        }
        return cells;
    }

    private int PhysicalSlot(InventoryList list, int cellIndex) =>
        list == InventoryList.Stash ? _stashPage * _stashPanel.AuthoredSlotCount + cellIndex : cellIndex;

    private int ResolveContextSlot(RectTransform anchor, bool fromStash)
    {
        RaidInventorySlotView[] cells = fromStash ? _stashCells : _loadoutCells;
        if (cells == null) return -1;
        for (int i = 0; i < cells.Length; i++)
            if (cells[i].transform == anchor)
                return PhysicalSlot(fromStash ? InventoryList.Stash : InventoryList.Loadout, i);
        return -1;
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

            views[index].SetDragLocation(DragSlotLocation.Equipment, slots[index]);
            views[index].SelectionRequested += OnEquipmentSlotSelected;
            BindEquipmentTooltipEvents(views[index]);
            
            views[index].DragStarted += p => DragStarted?.Invoke(p);
            views[index].DragUpdated += () => DragUpdated?.Invoke();
            views[index].DragEnded += b => DragEnded?.Invoke(b);
            views[index].DropReceived += (loc, targetSlot) => DropReceived?.Invoke(loc, targetSlot);
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

    public void SetActionsInteractable(bool interactable)
    {
        _actionsInteractable = interactable;
        if (!interactable) CloseQuantityDialog();
        if (_takeAllButton != null) _takeAllButton.interactable = interactable;
        if (_leaveAllButton != null) _leaveAllButton.interactable = interactable;
        if (_stashSortButton != null) _stashSortButton.interactable = interactable;
        if (_loadoutSortButton != null) _loadoutSortButton.interactable = interactable;
    }

    private void OnStashSortClicked() => SortRequested?.Invoke(InventoryList.Stash);
    private void OnLoadoutSortClicked() => SortRequested?.Invoke(InventoryList.Loadout);

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
        if (_gemLocksMutations || _contextMenu == null || !lootId.IsValid)
        {
            return;
        }

        _contextLootId = lootId;
        _contextIsFromStash = isFromStash;
        _contextSlot = ResolveContextSlot(anchor, isFromStash);
        _contextActions.Clear();
        string transferLabel = isFromStash
            ? "Mover todo al Inventario"
            : "Mover todo al Stash";
        _contextActions.Add(new LootContextActionDescriptor(MoveAllId, transferLabel, true, null));
        _contextActions.Add(new LootContextActionDescriptor(SplitId, "Dividir", CanSplitContext(), null));

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

        ContextActionsRequested?.Invoke(isFromStash ? InventoryList.Stash : InventoryList.Loadout,
            _contextSlot, lootId, _contextActions);
        _contextMenu.Show(_contextActions, anchor);
    }

    // Reopening starts at page one; refreshing while open preserves/clamps the current page.
    private void OnEnable()
    {
        _stashPage = 0;
        PresentStashPage();
    }

    private void OnDisable() => DismissStashInteraction();

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
        if (_gemLocksMutations || !_contextLootId.IsValid) return;
        if (actionId == SplitId)
        {
            if (!CanSplitContext()) return;
            _splitList = _contextIsFromStash ? InventoryList.Stash : InventoryList.Loadout;
            _splitSlot = _contextSlot;
            _splitLootId = _contextLootId;
            _splitQuantity = ContextData.Amount;
            QuantitySelectorDialogView dialog = QuantityDialog;
            if (dialog == null || !dialog.Open("Dividir", _splitQuantity)) CloseQuantityDialog();
            return;
        }
        foreach (var action in _contextActions)
        {
            if (action.Id != actionId || !action.IsEnabled || action.Provider == null) continue;
            // The provider captures the exact Town list/slot; definition resolution belongs to the presenter.
            GemContextActionRequested?.Invoke(action);
            _contextLootId = default;
            return;
        }
        if (actionId == MoveAllId)
        {
            LootId lootId = _contextLootId;
            bool fromStash = _contextIsFromStash;
            int sourceSlot = _contextSlot;
            if (sourceSlot >= 0)
                SlotTransferRequested?.Invoke(sourceSlot, lootId, fromStash, LootTransferQuantityMode.FullStack);
            // A synchronous confirmed refresh may clear context during dispatch, so use captured values.
            TransferRequested?.Invoke(lootId, fromStash, LootTransferQuantityMode.FullStack);
            _contextSlot = -1;
            _contextLootId = default;
            _contextIsFromStash = false;
            return;
        }

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        for (int index = 0; index < EquipActionIds.Length && index < slots.Length; index++)
        {
            if (actionId == EquipActionIds[index])
            {
                PreparedEquipmentAssignmentRequested?.Invoke(_contextLootId, slots[index], _contextIsFromStash);
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
