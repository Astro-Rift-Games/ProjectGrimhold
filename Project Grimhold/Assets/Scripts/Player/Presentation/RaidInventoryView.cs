using System;
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

    [SerializeField]
    private RectTransform _weaponSlotsRoot;

    [SerializeField]
    private RaidInventorySlotView _weaponSlotPrefab;

    [SerializeField, Min(0f)]
    private float _transferFeedbackDuration = 1.5f;

    private float _transferFeedbackRemaining;
    private RaidInventorySlotView _weaponSlot1View;
    private RaidInventorySlotView _weaponSlot2View;

    /// <summary>Local-only intention emitted when the enabled take-all control is activated.</summary>
    public event Action TakeAllRequested;
    public event Action<WeaponSlot> WeaponUnequipRequested;

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
        EnsureWeaponSlotViews();
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
        _weaponSlot1View?.Clear();
        _weaponSlot2View?.Clear();
        HideTransferFeedback();
    }

    public void PresentWeaponSlots(
        in RaidInventorySlotData slot1,
        in RaidInventorySlotData slot2,
        WeaponSlot activeSlot,
        bool canUnequip)
    {
        if (!EnsureWeaponSlotViews())
        {
            return;
        }

        _weaponSlot1View.PresentWeaponSlot(
            WeaponSlot.Slot1,
            in slot1,
            activeSlot == WeaponSlot.Slot1,
            canUnequip);
        _weaponSlot2View.PresentWeaponSlot(
            WeaponSlot.Slot2,
            in slot2,
            activeSlot == WeaponSlot.Slot2,
            canUnequip);
    }

    private bool EnsureWeaponSlotViews()
    {
        if (_weaponSlot1View != null && _weaponSlot2View != null)
        {
            return true;
        }

        if (_weaponSlotsRoot == null || _weaponSlotPrefab == null)
        {
            return false;
        }

        _weaponSlot1View = Instantiate(_weaponSlotPrefab, _weaponSlotsRoot);
        _weaponSlot2View = Instantiate(_weaponSlotPrefab, _weaponSlotsRoot);
        _weaponSlot1View.name = "WeaponSlot1";
        _weaponSlot2View.name = "WeaponSlot2";
        PositionWeaponSlot(_weaponSlot1View, -85f);
        PositionWeaponSlot(_weaponSlot2View, 85f);
        _weaponSlot1View.SelectionRequested += OnWeaponSlot1Selected;
        _weaponSlot2View.SelectionRequested += OnWeaponSlot2Selected;
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

    private void OnWeaponSlot1Selected(LootId _, LootTransferQuantityMode __) =>
        WeaponUnequipRequested?.Invoke(WeaponSlot.Slot1);

    private void OnWeaponSlot2Selected(LootId _, LootTransferQuantityMode __) =>
        WeaponUnequipRequested?.Invoke(WeaponSlot.Slot2);

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
