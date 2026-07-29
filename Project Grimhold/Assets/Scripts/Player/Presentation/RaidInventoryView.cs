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

    [SerializeField, Min(0f)]
    private float _transferFeedbackDuration = 1.5f;

    private float _transferFeedbackRemaining;

    /// <summary>Local-only intention emitted when the enabled take-all control is activated.</summary>
    public event Action TakeAllRequested;

    public bool IsOpen => _screenRoot != null && _screenRoot.activeSelf;
    public RaidLootPanelView PlayerPanel => _playerPanel;
    public RaidLootPanelView ContainerPanel => _containerPanel;

    /// <summary>Gets the contextual transfer-feedback label for presentation verification.</summary>
    public TMP_Text TransferFeedbackText => _transferFeedbackText;

    /// <summary>Gets the container take-all control for presentation verification.</summary>
    public Button TakeAllButton => _takeAllButton;

    private void Awake()
    {
        if (_takeAllButton != null)
        {
            _takeAllButton.onClick.AddListener(OnTakeAllClicked);
        }
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
        HideTransferFeedback();
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
