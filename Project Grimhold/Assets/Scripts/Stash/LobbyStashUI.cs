using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// The View component of the MVP pattern for the Lobby Stash.
/// Displays stash items visually without knowing about the underlying storage.
/// </summary>
public class LobbyStashUI : MonoBehaviour
{
    [SerializeField] private RectTransform _contentPanel;
    [SerializeField] private GameObject _itemSlotPrefab; // A simple prefab with a TextMeshProUGUI

    public void DisplayStash(IReadOnlyList<RaidInventorySlotData> items)
    {
        // Clear existing slots
        foreach (Transform child in _contentPanel)
        {
            Destroy(child.gameObject);
        }

        if (items == null || items.Count == 0)
        {
            return;
        }

        // Instantiate a slot for each item
        foreach (var item in items)
        {
            var slotObj = Instantiate(_itemSlotPrefab, _contentPanel);
            var slotView = slotObj.GetComponent<RaidInventorySlotView>();
            if (slotView != null)
            {
                slotView.Present(in item);
            }
        }
    }
}
