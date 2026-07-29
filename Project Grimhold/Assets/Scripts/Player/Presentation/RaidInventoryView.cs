using TMPro;
using UnityEngine;

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

    [SerializeField, Min(0f)]
    private float _transferFeedbackDuration = 1.5f;

    private float _transferFeedbackRemaining;

    public bool IsOpen => _screenRoot != null && _screenRoot.activeSelf;
    public RaidLootPanelView PlayerPanel => _playerPanel;
    public RaidLootPanelView ContainerPanel => _containerPanel;

    /// <summary>Gets the contextual transfer-feedback label for presentation verification.</summary>
    public TMP_Text TransferFeedbackText => _transferFeedbackText;

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
        if (_transferFeedbackText != null)
        {
            _transferFeedbackText.text = message ?? string.Empty;
        }

        if (_transferFeedbackRoot != null)
        {
            _transferFeedbackRoot.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        _transferFeedbackRemaining = string.IsNullOrWhiteSpace(message)
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
}
