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
    [SerializeField] private RectTransform _contentPanel; // The stash panel
    [SerializeField] private RectTransform _loadoutPanel; // The player's active loadout panel
    [SerializeField] private GameObject _itemSlotPrefab; // Prefab with RaidInventorySlotView

    [SerializeField] private Button _takeAllButton;
    [SerializeField] private Button _leaveAllButton;

    public event Action<LootId, bool, LootTransferQuantityMode> TransferRequested; // LootId, isFromStash, quantityMode
    public event Action TakeAllRequested;
    public event Action LeaveAllRequested;

    private void Awake()
    {
        if (_takeAllButton != null) _takeAllButton.onClick.AddListener(() => TakeAllRequested?.Invoke());
        if (_leaveAllButton != null) _leaveAllButton.onClick.AddListener(() => LeaveAllRequested?.Invoke());
    }

    private void OnDestroy()
    {
        if (_takeAllButton != null) _takeAllButton.onClick.RemoveAllListeners();
        if (_leaveAllButton != null) _leaveAllButton.onClick.RemoveAllListeners();
    }

    public void DisplayStash(IReadOnlyList<RaidInventorySlotData> items)
    {
        PopulatePanel(_contentPanel, items, true);
    }

    public void DisplayLoadout(IReadOnlyList<RaidInventorySlotData> items)
    {
        PopulatePanel(_loadoutPanel, items, false);
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
                slotView.SetInteraction(true, false);
                slotView.SelectionRequested += (lootId, mode) => OnSlotSelected(lootId, isStash, mode);
            }
        }
    }

    private void OnSlotSelected(LootId lootId, bool isFromStash, LootTransferQuantityMode mode)
    {
        TransferRequested?.Invoke(lootId, isFromStash, mode);
    }
}
