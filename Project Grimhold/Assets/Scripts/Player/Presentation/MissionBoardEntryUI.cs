using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI controller for a single mission entry listed on the Mission Board.
/// </summary>
public sealed class MissionBoardEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _rankText;
    [SerializeField] private TMP_Text _statusText; // e.g. "Completada", "Activa"
    [SerializeField] private Button _selectButton;
    [SerializeField] private Image _backgroundImage;

    [Header("Optional Styles")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _completedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color _activeColor = new Color(0.8f, 1f, 0.8f, 1f);

    private MissionDefinition _missionDef;
    private Action<MissionDefinition> _onSelected;

    public void Initialize(MissionDefinition def, MissionState? currentState, Action<MissionDefinition> onSelected)
    {
        _missionDef = def;
        _onSelected = onSelected;

        if (_titleText != null)
        {
            _titleText.text = def.Title;
        }

        if (_rankText != null)
        {
            _rankText.text = def.RankRequired.ToString();
        }

        // Handle visual states
        if (currentState.HasValue)
        {
            if (currentState.Value == MissionState.Activa || currentState.Value == MissionState.PendienteDeReclamar)
            {
                SetStatus("Activa", _activeColor);
                if (_selectButton != null) _selectButton.interactable = true; // allow view but maybe not accept
            }
            else if (currentState.Value == MissionState.Reclamada)
            {
                SetStatus("Completada", _completedColor);
                if (_selectButton != null) _selectButton.interactable = true;
            }
            else
            {
                SetStatus("", _normalColor);
                if (_selectButton != null) _selectButton.interactable = true;
            }
        }
        else
        {
            SetStatus("Disponible", _normalColor);
            if (_selectButton != null) _selectButton.interactable = true;
        }

        if (_selectButton != null)
        {
            _selectButton.onClick.RemoveAllListeners();
            _selectButton.onClick.AddListener(OnButtonClicked);
        }
    }

    private void SetStatus(string text, Color bgColor)
    {
        if (_statusText != null) _statusText.text = text;
        if (_backgroundImage != null) _backgroundImage.color = bgColor;
    }

    private void OnButtonClicked()
    {
        _onSelected?.Invoke(_missionDef);
    }
}
