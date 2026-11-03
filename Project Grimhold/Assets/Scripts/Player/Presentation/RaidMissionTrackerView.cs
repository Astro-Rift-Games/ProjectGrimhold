using System;
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
/// and visual highlights when objectives advance or complete.
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
    [SerializeField] private Color _activeColor = Color.white;
    [SerializeField] private Color _completedColor = new Color(0.95f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color _pulseColor = new Color(0.3f, 1f, 0.4f, 1f);

    private readonly List<GameObject> _instantiatedItems = new();
    private bool _isCollapsed;

    public bool IsCollapsed => _isCollapsed;

    public event Action CollapseToggled;

    private void Awake()
    {
        if (_toggleCollapseButton != null)
        {
            _toggleCollapseButton.onClick.RemoveAllListeners();
            _toggleCollapseButton.onClick.AddListener(ToggleCollapse);
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
            statusTxt.fontSize = 11f;
            statusTxt.fontStyle = FontStyles.Italic;
            statusTxt.text = "   ¡Completada! (Reclamar en Pueblo)";
            statusTxt.color = _completedColor;
        }

        _instantiatedItems.Add(itemGo);
    }

    public void PulseMission(MissionId missionId, bool isCompleted)
    {
        // Highlight feedback on completed or progressed mission
        if (_headerText != null)
        {
            _headerText.color = isCompleted ? _completedColor : _pulseColor;
        }
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
        ClearItems();
    }
}
