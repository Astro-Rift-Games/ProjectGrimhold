using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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

    [SerializeField]
    private EquipmentTooltipView _tooltipView;

    [SerializeField]
    private GameObject _equipmentPanelRoot;

    [Header("Equipment slots (authored in the prefab, never created at runtime)")]
    [FormerlySerializedAs("_weaponSlot1View"), SerializeField]
    private RaidInventorySlotView _weaponSetAMainHandView;

    [FormerlySerializedAs("_weaponSlot2View"), SerializeField]
    private RaidInventorySlotView _weaponSetBMainHandView;

    [SerializeField]
    private RaidInventorySlotView _weaponSetAOffHandView;

    [SerializeField]
    private RaidInventorySlotView _weaponSetBOffHandView;

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
    /// The eight serialized views in <see cref="EquipmentSlotRules.AllSlots"/>
    /// order. Built once from the named fields so the Inspector mapping cannot be mis-ordered.
    /// </summary>
    private RaidInventorySlotView[] _equipmentSlotViews;
    private bool _hasReportedMissingEquipmentViews;

    /// <summary>Local-only intention emitted when the enabled take-all control is activated.</summary>
    public event Action TakeAllRequested;
    public event Action<EquipmentSlot> EquipmentUnequipRequested;
    public event Action<EquipmentSlot, RectTransform> EquipmentContextRequested;

    public bool IsOpen => _screenRoot != null && _screenRoot.activeSelf;
    public RaidLootPanelView PlayerPanel => _playerPanel;
    public RaidLootPanelView ContainerPanel => _containerPanel;

    /// <summary>Gets the contextual transfer-feedback label for presentation verification.</summary>
    public TMP_Text TransferFeedbackText => _transferFeedbackText;

    /// <summary>Gets the container take-all control for presentation verification.</summary>
    public Button TakeAllButton => _takeAllButton;
    public RaidLootContextMenuView ContextMenu => _contextMenu;
    public EquipmentTooltipView TooltipView => _tooltipView;

    private void Awake()
    {
        if (_takeAllButton != null)
        {
            _takeAllButton.onClick.AddListener(OnTakeAllClicked);
        }
        BindPanelTooltipEvents(_playerPanel);
        BindPanelTooltipEvents(_containerPanel);
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
        _tooltipView?.Hide();
    }

    private void OnDestroy()
    {
        if (_takeAllButton != null)
        {
            _takeAllButton.onClick.RemoveListener(OnTakeAllClicked);
        }
        UnbindPanelTooltipEvents(_playerPanel);
        UnbindPanelTooltipEvents(_containerPanel);

        if (_equipmentSlotViews != null)
        {
            for (int index = 0; index < _equipmentSlotViews.Length; index++)
            {
                UnbindEquipmentTooltipEvents(_equipmentSlotViews[index]);
            }
        }
    }

    public void SetScreenVisible(bool visible)
    {
        if (!visible)
        {
            _tooltipView?.Hide();
        }
        if (_screenRoot != null && _screenRoot.activeSelf != visible)
        {
            _screenRoot.SetActive(visible);
        }
    }

    public void SetContainerPanelVisible(bool visible)
    {
        if (!visible)
        {
            _tooltipView?.Hide();
        }
        _containerPanel?.SetVisible(visible);
        if (!visible)
        {
            SetTakeAllInteractable(false);
        }
    }

    public void SetEquipmentPanelVisible(bool visible)
    {
        if (!visible)
        {
            _tooltipView?.Hide();
        }
        if (_equipmentPanelRoot != null && _equipmentPanelRoot.activeSelf != visible)
        {
            _equipmentPanelRoot.SetActive(visible);
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
        _tooltipView?.Hide();
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
    /// Projects the eight Equipment slots. Only Main Hand slots carry an active state;
    /// the armor slots show occupancy and offer the unequip intention.
    /// </summary>
    public void PresentEquipmentSlots(
        IReadOnlyList<RaidInventorySlotData> slotData,
        WeaponSetSlot activeSlot,
        bool canUnequip,
        bool setAOffHandBlocked = false,
        bool setBOffHandBlocked = false)
    {
        if (slotData == null || !EnsureEquipmentSlotViews())
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

            EquipmentSlot slot = slots[index];
            RaidInventorySlotData data = slotData[index];
            bool blocked = slot == EquipmentSlot.WeaponSetAOffHand && setAOffHandBlocked ||
                           slot == EquipmentSlot.WeaponSetBOffHand && setBOffHandBlocked;
            view.PresentEquipmentSlot(
                slot,
                in data,
                EquipmentSlotRules.GetWeaponSet(slot) == activeSlot && activeSlot != WeaponSetSlot.None,
                canUnequip,
                blocked);
        }
    }

    /// <summary>
    /// Binds the serialized Equipment views once. Nothing is instantiated: the panel and its eight
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
            _weaponSetAMainHandView, _weaponSetBMainHandView, _helmetView,
            _armorView, _glovesView, _bootsView,
            _weaponSetAOffHandView, _weaponSetBOffHandView
        };

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
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
            views[index].ContextRequested += (_, anchor) =>
            {
                _tooltipView?.Hide();
                EquipmentContextRequested?.Invoke(slot, anchor);
            };
            BindEquipmentTooltipEvents(views[index]);
        }

        _equipmentSlotViews = views;
        return true;
    }

    private void BindPanelTooltipEvents(RaidLootPanelView panel)
    {
        if (panel == null)
        {
            return;
        }

        panel.TooltipRequested += OnTooltipRequested;
        panel.TooltipDismissRequested += OnTooltipDismissRequested;
    }

    private void UnbindPanelTooltipEvents(RaidLootPanelView panel)
    {
        if (panel == null)
        {
            return;
        }

        panel.TooltipRequested -= OnTooltipRequested;
        panel.TooltipDismissRequested -= OnTooltipDismissRequested;
    }

    private void BindEquipmentTooltipEvents(RaidInventorySlotView view)
    {
        if (view == null)
        {
            return;
        }

        view.TooltipRequested += OnTooltipRequested;
        view.TooltipDismissRequested += OnTooltipDismissRequested;
    }

    private void UnbindEquipmentTooltipEvents(RaidInventorySlotView view)
    {
        if (view == null)
        {
            return;
        }

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
