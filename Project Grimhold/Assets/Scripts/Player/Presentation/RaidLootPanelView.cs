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

    [SerializeField]
    private GameObject _capacityFeedbackRoot;

    [SerializeField]
    private TMP_Text _capacityFeedbackText;

    [SerializeField, Min(0.01f)]
    private float _capacityPulseDuration = 0.35f;

    [SerializeField, Min(1f)]
    private float _capacityPulseScale = 1.03f;

    private readonly List<RaidInventorySlotView> _slots = new();
    private float _capacityPulseRemaining;
    private Vector3 _basePanelScale = Vector3.one;

    public event Action<LootId, LootTransferQuantityMode> SelectionRequested;
    public event Action<LootId, Vector2> ContextRequested;

    public Sprite PlaceholderIcon => _placeholderIcon;
    public int SlotCount => _slots.Count;

    /// <summary>Gets the panel-local capacity feedback label.</summary>
    public TMP_Text CapacityFeedbackText => _capacityFeedbackText;

    private void Awake()
    {
        if (_panelRoot != null)
        {
            _basePanelScale = _panelRoot.transform.localScale;
        }
        HideCapacityRejection();
    }

    private void Update()
    {
        if (_capacityPulseRemaining <= 0f || _panelRoot == null)
        {
            return;
        }

        _capacityPulseRemaining = Mathf.Max(0f, _capacityPulseRemaining - Time.deltaTime);
        float progress = 1f - _capacityPulseRemaining / _capacityPulseDuration;
        float pulse = Mathf.Sin(progress * Mathf.PI);
        _panelRoot.transform.localScale = _basePanelScale *
            Mathf.Lerp(1f, _capacityPulseScale, pulse);

        if (_capacityPulseRemaining <= 0f)
        {
            HideCapacityRejection();
        }
    }

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
            slot.ContextRequested += OnSlotContextRequested;
            slot.Clear();
            _slots.Add(slot);
        }

        while (_slots.Count > slotCount)
        {
            int last = _slots.Count - 1;
            RaidInventorySlotView slot = _slots[last];
            slot.SelectionRequested -= OnSlotSelectionRequested;
            slot.ContextRequested -= OnSlotContextRequested;
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
        return Present(
            slots,
            totalValue,
            showEmpty,
            interactive
                ? RaidLootSlotInteractionMode.Transfer
                : RaidLootSlotInteractionMode.ReadOnly,
            selectedLootId);
    }

    public bool Present(
        IReadOnlyList<RaidInventorySlotData> slots,
        long? totalValue,
        bool showEmpty,
        RaidLootSlotInteractionMode interactionMode,
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
                data.IsOccupied ? interactionMode : RaidLootSlotInteractionMode.ReadOnly,
                data.IsOccupied && data.LootId == selectedLootId);
        }

        PresentTotalValue(totalValue);

        SetState(_unavailableRoot, false);
        SetState(_emptyRoot, showEmpty);
        return true;
    }

    public void RefreshInteraction(bool interactive, LootId selectedLootId)
    {
        RefreshInteraction(
            interactive
                ? RaidLootSlotInteractionMode.Transfer
                : RaidLootSlotInteractionMode.ReadOnly,
            selectedLootId);
    }

    public void RefreshInteraction(
        RaidLootSlotInteractionMode interactionMode,
        LootId selectedLootId)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            RaidInventorySlotView slot = _slots[i];
            slot.SetInteraction(
                slot.IsOccupied ? interactionMode : RaidLootSlotInteractionMode.ReadOnly,
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
        HideCapacityRejection();
    }

    /// <summary>Shows a compact local-only capacity rejection on this destination panel.</summary>
    public void ShowCapacityRejection()
    {
        if (_capacityFeedbackText != null)
        {
            _capacityFeedbackText.text = "Lleno";
        }

        SetState(_capacityFeedbackRoot, true);
        _capacityPulseRemaining = _capacityPulseDuration;
    }

    /// <summary>Clears the contextual capacity feedback and restores the panel pose.</summary>
    public void HideCapacityRejection()
    {
        _capacityPulseRemaining = 0f;
        if (_panelRoot != null)
        {
            _panelRoot.transform.localScale = _basePanelScale;
        }
        SetState(_capacityFeedbackRoot, false);
    }

    private void OnDestroy()
    {
        HideCapacityRejection();
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null)
            {
                _slots[i].SelectionRequested -= OnSlotSelectionRequested;
                _slots[i].ContextRequested -= OnSlotContextRequested;
            }
        }
    }

    private void OnSlotSelectionRequested(LootId lootId, LootTransferQuantityMode quantityMode)
    {
        SelectionRequested?.Invoke(lootId, quantityMode);
    }

    private void OnSlotContextRequested(LootId lootId, Vector2 screenPosition)
    {
        ContextRequested?.Invoke(lootId, screenPosition);
    }

    private static void SetState(GameObject root, bool active)
    {
        if (root != null && root.activeSelf != active)
        {
            root.SetActive(active);
        }
    }
}
