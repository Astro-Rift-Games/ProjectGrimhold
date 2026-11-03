using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct MissionTrackerObjectiveViewModel
{
    public readonly string Description;
    public readonly int CurrentAmount;
    public readonly int RequiredAmount;
    public readonly bool IsCompleted;

    public MissionTrackerObjectiveViewModel(string description, int currentAmount, int requiredAmount, bool isCompleted)
    {
        Description = description;
        CurrentAmount = currentAmount;
        RequiredAmount = requiredAmount;
        IsCompleted = isCompleted;
    }
}

public readonly struct MissionTrackerItemViewModel
{
    public readonly MissionId Id;
    public readonly string Title;
    public readonly MissionState State;
    public readonly IReadOnlyList<MissionTrackerObjectiveViewModel> Objectives;

    public MissionTrackerItemViewModel(
        MissionId id,
        string title,
        MissionState state,
        IReadOnlyList<MissionTrackerObjectiveViewModel> objectives)
    {
        Id = id;
        Title = title;
        State = state;
        Objectives = objectives;
    }
}

/// <summary>
/// Passive HUD view that presents active missions during a raid.
/// Supports collapsible widget state, real-time objective counters,
/// visual highlights, and toast notifications when objectives advance or complete.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidMissionTrackerView : MonoBehaviour
{
    [Header("Hierarchy")]
    [SerializeField] private GameObject _root;
    [SerializeField] private GameObject _contentRoot;
    [SerializeField] private Button _toggleCollapseButton;
    [SerializeField] private TMP_Text _headerText;
    [SerializeField] private Transform _itemsContainer;

    [Header("Styling")]
    [SerializeField] private TMP_FontAsset _fontAsset;
    [SerializeField] private Color _activeColor = Color.white;
    [SerializeField] private Color _completedColor = new Color(0.95f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color _pulseColor = new Color(0.3f, 1f, 0.4f, 1f);

    [Header("Popup Toast Notification")]
    [SerializeField] private GameObject _popupRoot;
    [SerializeField] private CanvasGroup _popupCanvasGroup;
    [SerializeField] private TMP_Text _popupHeaderText;
    [SerializeField] private TMP_Text _popupTitleText;
    [SerializeField] private TMP_Text _popupDetailText;
    [SerializeField] private Image _popupBorderImage;
    [SerializeField] private float _popupDuration = 3.0f;

    private readonly List<GameObject> _instantiatedItems = new();
    private bool _isCollapsed;
    private Coroutine _popupCoroutine;

    public bool IsCollapsed => _isCollapsed;
    public bool IsPopupActive => _popupRoot != null && _popupRoot.activeSelf;
    public TMP_Text PopupHeaderText => _popupHeaderText;
    public TMP_Text PopupTitleText => _popupTitleText;
    public TMP_Text PopupDetailText => _popupDetailText;

    public event Action CollapseToggled;

    private void Awake()
    {
        if (_toggleCollapseButton != null)
        {
            _toggleCollapseButton.onClick.RemoveAllListeners();
            _toggleCollapseButton.onClick.AddListener(ToggleCollapse);
        }

        if (_popupRoot != null)
        {
            _popupRoot.SetActive(false);
        }
    }

    public void ToggleCollapse()
    {
        _isCollapsed = !_isCollapsed;
        if (_contentRoot != null)
        {
            _contentRoot.SetActive(!_isCollapsed);
        }
        UpdateHeaderText(_instantiatedItems.Count);
        CollapseToggled?.Invoke();
    }

    public void SetCollapsed(bool collapsed)
    {
        _isCollapsed = collapsed;
        if (_contentRoot != null)
        {
            _contentRoot.SetActive(!_isCollapsed);
        }
        UpdateHeaderText(_instantiatedItems.Count);
    }

    public void Present(IReadOnlyList<MissionTrackerItemViewModel> items)
    {
        ClearItems();

        int count = items != null ? items.Count : 0;
        UpdateHeaderText(count);

        if (count == 0)
        {
            if (_root != null) _root.SetActive(false);
            return;
        }

        if (_root != null) _root.SetActive(true);
        if (_itemsContainer == null) return;

        for (int i = 0; i < count; i++)
        {
            var item = items[i];
            CreateItemView(item);
        }
    }

    private void UpdateHeaderText(int count)
    {
        if (_headerText != null)
        {
            string arrow = _isCollapsed ? "▼" : "▲";
            _headerText.text = $"Misiones ({count}) [{arrow}]";
        }
    }

    private void CreateItemView(MissionTrackerItemViewModel model)
    {
        var itemGo = new GameObject($"MissionItem_{model.Id.Value}");
        itemGo.transform.SetParent(_itemsContainer, false);

        var vertLayout = itemGo.AddComponent<VerticalLayoutGroup>();
        vertLayout.childControlHeight = true;
        vertLayout.childControlWidth = true;
        vertLayout.childForceExpandHeight = false;
        vertLayout.childForceExpandWidth = true;
        vertLayout.spacing = 2f;

        // Title text
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(itemGo.transform, false);
        var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        if (_fontAsset != null) titleTxt.font = _fontAsset;
        titleTxt.fontSize = 13f;
        titleTxt.fontStyle = FontStyles.Bold;

        bool isClaimable = model.State == MissionState.PendienteDeReclamar;
        if (isClaimable)
        {
            titleTxt.text = $"★ {model.Title}";
            titleTxt.color = _completedColor;
        }
        else
        {
            titleTxt.text = $"• {model.Title}";
            titleTxt.color = _activeColor;
        }

        // Objectives
        if (model.Objectives != null)
        {
            for (int objIdx = 0; objIdx < model.Objectives.Count; objIdx++)
            {
                var obj = model.Objectives[objIdx];
                var objGo = new GameObject($"Objective_{objIdx}");
                objGo.transform.SetParent(itemGo.transform, false);
                var objTxt = objGo.AddComponent<TextMeshProUGUI>();
                if (_fontAsset != null) objTxt.font = _fontAsset;
                objTxt.fontSize = 11f;

                string checkmark = obj.IsCompleted ? "✔ " : "- ";
                objTxt.text = $"   {checkmark}{obj.Description}: {obj.CurrentAmount}/{obj.RequiredAmount}";
                objTxt.color = obj.IsCompleted ? _completedColor : new Color(0.85f, 0.85f, 0.85f, 1f);
            }
        }

        if (isClaimable)
        {
            var statusGo = new GameObject("StatusNotice");
            statusGo.transform.SetParent(itemGo.transform, false);
            var statusTxt = statusGo.AddComponent<TextMeshProUGUI>();
            if (_fontAsset != null) statusTxt.font = _fontAsset;
            statusTxt.fontSize = 11f;
            statusTxt.fontStyle = FontStyles.Italic;
            statusTxt.text = "   ¡Completada! (Reclamar en Pueblo)";
            statusTxt.color = _completedColor;
        }

        _instantiatedItems.Add(itemGo);
    }

    public void PulseMission(MissionId missionId, bool isCompleted)
    {
        if (_headerText != null)
        {
            _headerText.color = isCompleted ? _completedColor : _pulseColor;
        }
    }

    public void ShowProgressPopup(string header, string title, string detail, bool isCompletion)
    {
        if (_popupRoot == null) return;

        if (_popupCoroutine != null)
        {
            StopCoroutine(_popupCoroutine);
        }

        if (gameObject.activeInHierarchy)
        {
            _popupCoroutine = StartCoroutine(DoShowPopup(header, title, detail, isCompletion));
        }
        else
        {
            // Immediate sync fallback if GameObject is inactive
            ApplyPopupData(header, title, detail, isCompletion);
            _popupRoot.SetActive(true);
            if (_popupCanvasGroup != null) _popupCanvasGroup.alpha = 1f;
        }
    }

    private void ApplyPopupData(string header, string title, string detail, bool isCompletion)
    {
        Color highlight = isCompletion ? _completedColor : _pulseColor;

        if (_popupHeaderText != null)
        {
            if (_fontAsset != null) _popupHeaderText.font = _fontAsset;
            _popupHeaderText.text = header;
            _popupHeaderText.color = highlight;
        }

        if (_popupTitleText != null)
        {
            if (_fontAsset != null) _popupTitleText.font = _fontAsset;
            _popupTitleText.text = title;
        }

        if (_popupDetailText != null)
        {
            if (_fontAsset != null) _popupDetailText.font = _fontAsset;
            _popupDetailText.text = detail;
        }

        if (_popupBorderImage != null)
        {
            _popupBorderImage.color = highlight;
        }
    }

    private IEnumerator DoShowPopup(string header, string title, string detail, bool isCompletion)
    {
        ApplyPopupData(header, title, detail, isCompletion);

        _popupRoot.SetActive(true);

        if (_popupCanvasGroup != null)
        {
            _popupCanvasGroup.alpha = 0f;
            float fadeIn = 0.2f;
            for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
            {
                _popupCanvasGroup.alpha = Mathf.Clamp01(t / fadeIn);
                yield return null;
            }
            _popupCanvasGroup.alpha = 1f;
        }

        float elapsed = 0f;
        while (elapsed < _popupDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_popupCanvasGroup != null)
        {
            float fadeOut = 0.35f;
            for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
            {
                _popupCanvasGroup.alpha = Mathf.Clamp01(1f - (t / fadeOut));
                yield return null;
            }
            _popupCanvasGroup.alpha = 0f;
        }

        _popupRoot.SetActive(false);
        _popupCoroutine = null;
    }

    private void ClearItems()
    {
        for (int i = 0; i < _instantiatedItems.Count; i++)
        {
            if (_instantiatedItems[i] != null) Destroy(_instantiatedItems[i]);
        }
        _instantiatedItems.Clear();
    }

    private void OnDestroy()
    {
        if (_popupCoroutine != null)
        {
            StopCoroutine(_popupCoroutine);
            _popupCoroutine = null;
        }
        ClearItems();
    }
}
