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

    [Tooltip("Every slot this panel can ever show, authored in the prefab. Nothing is created at runtime.")]
    [SerializeField]
    private RaidInventorySlotView[] _authoredSlots = Array.Empty<RaidInventorySlotView>();

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

    /// <summary>The authored slots currently shown, always a prefix of <c>_authoredSlots</c>.</summary>
    private readonly List<RaidInventorySlotView> _slots = new();
    private float _capacityPulseRemaining;
    private Vector3 _basePanelScale = Vector3.one;
    private bool _hasBoundAuthoredSlots;
    private bool _hasReportedSlotShortage;

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

    /// <summary>
    /// Shows the first <paramref name="slotCount"/> authored slots and hides the rest. The pool is
    /// authored in the prefab, so a request larger than the pool fails instead of creating a slot.
    /// </summary>
    public bool EnsureSlotCount(int slotCount)
    {
        if (slotCount <= 0 || !BindAuthoredSlots())
        {
            return false;
        }

        if (slotCount > _authoredSlots.Length)
        {
            if (!_hasReportedSlotShortage)
            {
                _hasReportedSlotShortage = true;
                Debug.LogError(
                    $"{name} needs {slotCount} slots but only {_authoredSlots.Length} are authored in the prefab.",
                    this);
            }

            return false;
        }

        _slots.Clear();
        for (int index = 0; index < _authoredSlots.Length; index++)
        {
            RaidInventorySlotView slot = _authoredSlots[index];
            bool used = index < slotCount;
            if (slot.gameObject.activeSelf != used)
            {
                slot.gameObject.SetActive(used);
            }

            if (used)
            {
                _slots.Add(slot);
            }
        }

        return true;
    }

    /// <summary>Subscribes the authored slots exactly once.</summary>
    private bool BindAuthoredSlots()
    {
        if (_hasBoundAuthoredSlots)
        {
            return true;
        }

        if (_authoredSlots == null || _authoredSlots.Length == 0)
        {
            Debug.LogError($"{name} has no authored slots assigned.", this);
            return false;
        }

        for (int index = 0; index < _authoredSlots.Length; index++)
        {
            if (_authoredSlots[index] == null)
            {
                Debug.LogError($"{name} has an unassigned authored slot at index {index}.", this);
                return false;
            }
        }

        for (int index = 0; index < _authoredSlots.Length; index++)
        {
            _authoredSlots[index].SelectionRequested += OnSlotSelectionRequested;
            _authoredSlots[index].ContextRequested += OnSlotContextRequested;
            _authoredSlots[index].Clear();
        }

        _hasBoundAuthoredSlots = true;
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
        if (_authoredSlots == null)
        {
            return;
        }

        for (int i = 0; i < _authoredSlots.Length; i++)
        {
            if (_authoredSlots[i] != null)
            {
                _authoredSlots[i].SelectionRequested -= OnSlotSelectionRequested;
                _authoredSlots[i].ContextRequested -= OnSlotContextRequested;
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
