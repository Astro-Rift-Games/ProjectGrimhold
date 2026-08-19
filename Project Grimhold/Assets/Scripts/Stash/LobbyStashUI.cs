using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The View component of the MVP pattern for the Lobby Stash and Loadout.
/// Displays stash and loadout items visually and emits interaction events.
/// </summary>
public class LobbyStashUI : MonoBehaviour
{
    private static readonly LootContextActionId MoveAllId = new("town.loadout.move-all");
    private static readonly LootContextActionId AssignSlot1Id = new("town.weapon.assign-slot-1");
    private static readonly LootContextActionId AssignSlot2Id = new("town.weapon.assign-slot-2");

    [SerializeField] private RectTransform _contentPanel; // The stash panel
    [SerializeField] private RectTransform _loadoutPanel; // The player's active loadout panel
    [SerializeField] private GameObject _itemSlotPrefab; // Prefab with RaidInventorySlotView

    [SerializeField] private Button _takeAllButton;
    [SerializeField] private Button _leaveAllButton;
    [SerializeField] private RectTransform _weaponSlotsRoot;
    [SerializeField] private RaidLootContextMenuView _contextMenu;

    private RaidInventorySlotView _weaponSlot1View;
    private RaidInventorySlotView _weaponSlot2View;
    private LootId _contextLootId;
    private readonly List<LootContextActionDescriptor> _contextActions = new();

    public event Action<LootId, bool, LootTransferQuantityMode> TransferRequested; // LootId, isFromStash, quantityMode
    public event Action TakeAllRequested;
    public event Action LeaveAllRequested;
    public event Action<LootId, WeaponSlot> PreparedWeaponAssignmentRequested;
    public event Action<WeaponSlot> PreparedWeaponClearRequested;

    private void Awake()
    {
        if (_takeAllButton != null) _takeAllButton.onClick.AddListener(() => TakeAllRequested?.Invoke());
        if (_leaveAllButton != null) _leaveAllButton.onClick.AddListener(() => LeaveAllRequested?.Invoke());
        if (_contextMenu != null) _contextMenu.ActionRequested += OnContextActionRequested;
        EnsureWeaponSlots();
    }

    private void OnDestroy()
    {
        if (_takeAllButton != null) _takeAllButton.onClick.RemoveAllListeners();
        if (_leaveAllButton != null) _leaveAllButton.onClick.RemoveAllListeners();
        if (_contextMenu != null) _contextMenu.ActionRequested -= OnContextActionRequested;
    }

    public void DisplayStash(IReadOnlyList<RaidInventorySlotData> items)
    {
        PopulatePanel(_contentPanel, items, true);
    }

    public void DisplayLoadout(IReadOnlyList<RaidInventorySlotData> items)
    {
        PopulatePanel(_loadoutPanel, items, false);
    }

    public void DisplayPreparedWeapons(
        in RaidInventorySlotData slot1,
        in RaidInventorySlotData slot2)
    {
        if (!EnsureWeaponSlots()) return;
        _weaponSlot1View.PresentWeaponSlot(WeaponSlot.Slot1, in slot1, false, slot1.IsOccupied);
        _weaponSlot2View.PresentWeaponSlot(WeaponSlot.Slot2, in slot2, false, slot2.IsOccupied);
    }

    private void PopulatePanel(RectTransform panel, IReadOnlyList<RaidInventorySlotData> items, bool isStash)
    {
        if (panel == null) return;

        // Clear existing slots
        foreach (Transform child in panel)
        {
            var slotView = child.GetComponent<RaidInventorySlotView>();
            if (slotView != null)
            {
                slotView.SelectionRequested -= (lootId, mode) => OnSlotSelected(lootId, isStash, mode);
            }
            Destroy(child.gameObject);
        }

        if (items == null || items.Count == 0)
        {
            return;
        }

        // Instantiate a slot for each item
        foreach (var item in items)
        {
            var slotObj = Instantiate(_itemSlotPrefab, panel);
            var slotView = slotObj.GetComponent<RaidInventorySlotView>();
            if (slotView != null)
            {
                slotView.Present(in item);
                slotView.SetInteraction(
                    isStash
                        ? RaidLootSlotInteractionMode.Transfer
                        : RaidLootSlotInteractionMode.TransferWithContextMenu,
                    false);
                slotView.SelectionRequested += (lootId, mode) => OnSlotSelected(lootId, isStash, mode);
                if (!isStash)
                {
                    slotView.ContextRequested += OnLoadoutContextRequested;
                }
            }
        }
    }

    private void OnSlotSelected(LootId lootId, bool isFromStash, LootTransferQuantityMode mode)
    {
        TransferRequested?.Invoke(lootId, isFromStash, mode);
    }

    private void OnLoadoutContextRequested(LootId lootId, Vector2 screenPosition)
    {
        if (_contextMenu == null || !lootId.IsValid) return;
        _contextLootId = lootId;
        _contextActions.Clear();
        _contextActions.Add(new LootContextActionDescriptor(MoveAllId, "Mover todo al Stash", true, null));
        _contextActions.Add(new LootContextActionDescriptor(AssignSlot1Id, "Asignar Weapon Slot 1", true, null));
        _contextActions.Add(new LootContextActionDescriptor(AssignSlot2Id, "Asignar Weapon Slot 2", true, null));
        _contextMenu.Show(_contextActions, screenPosition);
    }

    private void OnContextActionRequested(LootContextActionId actionId)
    {
        _contextMenu?.Hide();
        if (!_contextLootId.IsValid) return;
        if (actionId == MoveAllId)
        {
            TransferRequested?.Invoke(_contextLootId, false, LootTransferQuantityMode.FullStack);
        }
        else if (actionId == AssignSlot1Id)
        {
            PreparedWeaponAssignmentRequested?.Invoke(_contextLootId, WeaponSlot.Slot1);
        }
        else if (actionId == AssignSlot2Id)
        {
            PreparedWeaponAssignmentRequested?.Invoke(_contextLootId, WeaponSlot.Slot2);
        }
        _contextLootId = default;
    }

    private bool EnsureWeaponSlots()
    {
        if (_weaponSlot1View != null && _weaponSlot2View != null) return true;
        if (_weaponSlotsRoot == null || _itemSlotPrefab == null) return false;
        _weaponSlot1View = Instantiate(_itemSlotPrefab, _weaponSlotsRoot)
            .GetComponent<RaidInventorySlotView>();
        _weaponSlot2View = Instantiate(_itemSlotPrefab, _weaponSlotsRoot)
            .GetComponent<RaidInventorySlotView>();
        if (_weaponSlot1View == null || _weaponSlot2View == null) return false;
        _weaponSlot1View.name = "PreparedWeaponSlot1";
        _weaponSlot2View.name = "PreparedWeaponSlot2";
        PositionWeaponSlot(_weaponSlot1View, -85f);
        PositionWeaponSlot(_weaponSlot2View, 85f);
        _weaponSlot1View.SelectionRequested += (_, _) =>
            PreparedWeaponClearRequested?.Invoke(WeaponSlot.Slot1);
        _weaponSlot2View.SelectionRequested += (_, _) =>
            PreparedWeaponClearRequested?.Invoke(WeaponSlot.Slot2);
        return true;
    }

    private static void PositionWeaponSlot(RaidInventorySlotView view, float x)
    {
        LayoutElement layoutElement = view.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = view.gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.ignoreLayout = true;

        if (view.transform is RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, -48f);
        }
    }
}
