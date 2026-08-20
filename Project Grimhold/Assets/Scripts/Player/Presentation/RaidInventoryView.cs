using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the combined raid inventory screen and composes player and container panel views.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidInventoryView : MonoBehaviour
{
    [SerializeField]
    private GameObject _screenRoot;

    [SerializeField]
    private RaidLootPanelView _playerPanel;

    [SerializeField]
    private RaidLootPanelView _containerPanel;

    [SerializeField]
    private GameObject _transferFeedbackRoot;

    [SerializeField]
    private TMP_Text _transferFeedbackText;

    [SerializeField]
    private Button _takeAllButton;

    [SerializeField]
    private RaidLootContextMenuView _contextMenu;

    [Header("Equipment slots (authored in the prefab, never created at runtime)")]
    [SerializeField]
    private RaidInventorySlotView _weaponSlot1View;

    [SerializeField]
    private RaidInventorySlotView _weaponSlot2View;

    [SerializeField]
    private RaidInventorySlotView _helmetView;

    [SerializeField]
    private RaidInventorySlotView _armorView;

    [SerializeField]
    private RaidInventorySlotView _glovesView;

    [SerializeField]
    private RaidInventorySlotView _bootsView;

    [SerializeField, Min(0f)]
    private float _transferFeedbackDuration = 1.5f;

    private float _transferFeedbackRemaining;

    /// <summary>
    /// The six serialized views in <see cref="PlayerWeaponEquipmentNetworkController.AllSlots"/>
    /// order. Built once from the named fields so the Inspector mapping cannot be mis-ordered.
    /// </summary>
    private RaidInventorySlotView[] _equipmentSlotViews;
    private bool _hasReportedMissingEquipmentViews;

    /// <summary>Local-only intention emitted when the enabled take-all control is activated.</summary>
    public event Action TakeAllRequested;
    public event Action<EquipmentSlot> EquipmentUnequipRequested;

    public bool IsOpen => _screenRoot != null && _screenRoot.activeSelf;
    public RaidLootPanelView PlayerPanel => _playerPanel;
    public RaidLootPanelView ContainerPanel => _containerPanel;

    /// <summary>Gets the contextual transfer-feedback label for presentation verification.</summary>
    public TMP_Text TransferFeedbackText => _transferFeedbackText;

    /// <summary>Gets the container take-all control for presentation verification.</summary>
    public Button TakeAllButton => _takeAllButton;
    public RaidLootContextMenuView ContextMenu => _contextMenu;

    private void Awake()
    {
        if (_takeAllButton != null)
        {
            _takeAllButton.onClick.AddListener(OnTakeAllClicked);
        }
        EnsureEquipmentSlotViews();
    }

    private void Update()
    {
        if (_transferFeedbackRemaining <= 0f)
        {
            return;
        }

        _transferFeedbackRemaining -= Time.deltaTime;
        if (_transferFeedbackRemaining <= 0f)
        {
            HideTransferFeedback();
        }
    }

    private void OnDisable()
    {
        HideTransferFeedback();
    }

    private void OnDestroy()
    {
        if (_takeAllButton != null)
        {
            _takeAllButton.onClick.RemoveListener(OnTakeAllClicked);
        }
    }

    public void SetScreenVisible(bool visible)
    {
        if (_screenRoot != null && _screenRoot.activeSelf != visible)
        {
            _screenRoot.SetActive(visible);
        }
    }

    public void SetContainerPanelVisible(bool visible)
    {
        _containerPanel?.SetVisible(visible);
        if (!visible)
        {
            SetTakeAllInteractable(false);
        }
    }

    /// <summary>Updates whether the local take-all intention can be started.</summary>
    public void SetTakeAllInteractable(bool interactable)
    {
        if (_takeAllButton != null)
        {
            _takeAllButton.interactable = interactable;
        }
    }

    public void ClearContent()
    {
        _playerPanel?.ClearContent();
        _containerPanel?.ClearContent();
        _contextMenu?.Hide();
        if (EnsureEquipmentSlotViews())
        {
            for (int index = 0; index < _equipmentSlotViews.Length; index++)
            {
                _equipmentSlotViews[index]?.Clear();
            }
        }
        HideTransferFeedback();
    }

    /// <summary>
    /// Projects the six Equipment slots. Only the two weapon slots carry an active state;
    /// the armor slots show occupancy and offer the unequip intention.
    /// </summary>
    public void PresentEquipmentSlots(
        IReadOnlyList<RaidInventorySlotData> slotData,
        WeaponSlot activeSlot,
        bool canUnequip)
    {
        if (slotData == null || !EnsureEquipmentSlotViews())
        {
            return;
        }

        EquipmentSlot[] slots = PlayerWeaponEquipmentNetworkController.AllSlots;
        int count = Mathf.Min(slots.Length, slotData.Count);
        for (int index = 0; index < count; index++)
        {
            RaidInventorySlotView view = _equipmentSlotViews[index];
            if (view == null)
            {
                continue;
            }

            EquipmentSlot slot = slots[index];
            RaidInventorySlotData data = slotData[index];
            view.PresentEquipmentSlot(
                slot,
                in data,
                EquipmentSlotRules.ToWeaponSlot(slot) == activeSlot && activeSlot != WeaponSlot.None,
                canUnequip);
        }
    }

    /// <summary>
    /// Binds the serialized Equipment views once. Nothing is instantiated: the panel and its six
    /// slots are authored in the prefab so the layout stays fully editable in the Inspector.
    /// </summary>
    private bool EnsureEquipmentSlotViews()
    {
        if (_equipmentSlotViews != null)
        {
            return true;
        }

        var views = new[]
        {
            _weaponSlot1View, _weaponSlot2View, _helmetView,
            _armorView, _glovesView, _bootsView
        };

        EquipmentSlot[] slots = PlayerWeaponEquipmentNetworkController.AllSlots;
        if (views.Length != slots.Length)
        {
            Debug.LogError($"{nameof(RaidInventoryView)} exposes {views.Length} equipment views for {slots.Length} slots.", this);
            return false;
        }

        for (int index = 0; index < views.Length; index++)
        {
            if (views[index] == null)
            {
                ReportMissingEquipmentViews(slots[index]);
                continue;
            }

            EquipmentSlot slot = slots[index];
            views[index].SelectionRequested += (_, __) => EquipmentUnequipRequested?.Invoke(slot);
        }

        _equipmentSlotViews = views;
        return true;
    }

    private void ReportMissingEquipmentViews(EquipmentSlot slot)
    {
        if (_hasReportedMissingEquipmentViews)
        {
            return;
        }

        _hasReportedMissingEquipmentViews = true;
        Debug.LogError(
            $"{nameof(RaidInventoryView)} has no serialized view for {slot}. Assign every Equipment slot view on the prefab.",
            this);
    }

    /// <summary>Shows a temporary, local-only reason for a rejected transfer request.</summary>
    public void ShowTransferFeedback(string message)
    {
        SetTransferFeedback(message, true);
    }

    /// <summary>Shows local transfer feedback until it is explicitly replaced or cleared.</summary>
    public void ShowPersistentTransferFeedback(string message)
    {
        SetTransferFeedback(message, false);
    }

    private void SetTransferFeedback(string message, bool autoHide)
    {
        if (_transferFeedbackText != null)
        {
            _transferFeedbackText.text = message ?? string.Empty;
        }

        if (_transferFeedbackRoot != null)
        {
            _transferFeedbackRoot.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        _transferFeedbackRemaining = string.IsNullOrWhiteSpace(message) || !autoHide
            ? 0f
            : _transferFeedbackDuration;
    }

    /// <summary>Clears contextual transfer feedback without changing inventory state.</summary>
    public void HideTransferFeedback()
    {
        _transferFeedbackRemaining = 0f;
        if (_transferFeedbackRoot != null)
        {
            _transferFeedbackRoot.SetActive(false);
        }
    }

    private void OnTakeAllClicked()
    {
        if (_takeAllButton != null && _takeAllButton.interactable)
        {
            TakeAllRequested?.Invoke();
        }
    }
}
