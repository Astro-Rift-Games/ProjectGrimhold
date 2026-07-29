using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Renders one reusable read-only or selectable loot collection with a stable slot pool.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidLootPanelView : MonoBehaviour
{
    [SerializeField]
    private GameObject _panelRoot;

    [SerializeField]
    private RectTransform _slotContainer;

    [SerializeField]
    private RaidInventorySlotView _slotPrefab;

    [SerializeField]
    private TMP_Text _totalValueText;

    [SerializeField]
    private GameObject _unavailableRoot;

    [SerializeField]
    private GameObject _emptyRoot;

    [SerializeField]
    private Sprite _placeholderIcon;

    private readonly List<RaidInventorySlotView> _slots = new();

    public event Action<LootId, LootTransferQuantityMode> SelectionRequested;

    public Sprite PlaceholderIcon => _placeholderIcon;
    public int SlotCount => _slots.Count;

    /// <summary>Gets the panel-local total value label for presentation verification.</summary>
    public TMP_Text TotalValueText => _totalValueText;

    public void SetVisible(bool visible)
    {
        if (_panelRoot != null && _panelRoot.activeSelf != visible)
        {
            _panelRoot.SetActive(visible);
        }
    }

    public bool EnsureSlotCount(int slotCount)
    {
        if (slotCount <= 0 || _slotContainer == null || _slotPrefab == null)
        {
            return false;
        }

        while (_slots.Count < slotCount)
        {
            RaidInventorySlotView slot = Instantiate(_slotPrefab, _slotContainer);
            slot.SelectionRequested += OnSlotSelectionRequested;
            slot.Clear();
            _slots.Add(slot);
        }

        while (_slots.Count > slotCount)
        {
            int last = _slots.Count - 1;
            RaidInventorySlotView slot = _slots[last];
            slot.SelectionRequested -= OnSlotSelectionRequested;
            _slots.RemoveAt(last);
            Destroy(slot.gameObject);
        }

        return true;
    }

    public bool Present(
        IReadOnlyList<RaidInventorySlotData> slots,
        long? totalValue,
        bool showEmpty,
        bool interactive,
        LootId selectedLootId)
    {
        if (slots == null || slots.Count != _slots.Count)
        {
            return false;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            RaidInventorySlotData data = slots[i];
            _slots[i].Present(in data);
            _slots[i].SetInteraction(
                interactive && data.IsOccupied,
                data.IsOccupied && data.LootId == selectedLootId);
        }

        PresentTotalValue(totalValue);

        SetState(_unavailableRoot, false);
        SetState(_emptyRoot, showEmpty);
        return true;
    }

    public void RefreshInteraction(bool interactive, LootId selectedLootId)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            RaidInventorySlotView slot = _slots[i];
            slot.SetInteraction(
                interactive && slot.IsOccupied,
                slot.IsOccupied && slot.LootId == selectedLootId);
        }
    }

    /// <summary>Presents the complete total value or an unavailable placeholder.</summary>
    public void PresentTotalValue(long? totalValue)
    {
        if (_totalValueText == null)
        {
            return;
        }

        string text = totalValue.HasValue ? $"Valor: {totalValue.Value}" : "Valor: —";
        if (_totalValueText.text != text)
        {
            _totalValueText.text = text;
        }
    }

    public void ShowUnavailable()
    {
        ClearContent();
        SetState(_unavailableRoot, true);
    }

    public void ClearContent()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].Clear();
        }

        PresentTotalValue(null);

        SetState(_unavailableRoot, false);
        SetState(_emptyRoot, false);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null)
            {
                _slots[i].SelectionRequested -= OnSlotSelectionRequested;
            }
        }
    }

    private void OnSlotSelectionRequested(LootId lootId, LootTransferQuantityMode quantityMode)
    {
        SelectionRequested?.Invoke(lootId, quantityMode);
    }

    private static void SetState(GameObject root, bool active)
    {
        if (root != null && root.activeSelf != active)
        {
            root.SetActive(active);
        }
    }
}
