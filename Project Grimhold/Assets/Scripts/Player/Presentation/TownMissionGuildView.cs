using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum GuildViewTab
{
    Available = 0,
    Active = 1
}

/// <summary>
/// Passive local UI Canvas of the Mission Guild in Town.
/// Displays available contracts, active contracts, mission details, and action buttons
/// (Accept, Abandon, Claim). Dispatches user intentions to <see cref="TownMissionGuildPresenter"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownMissionGuildView : MonoBehaviour
{
    private const int OverlaySortingOrder = 110;

    [Header("Header")]
    [SerializeField] private TMP_Text _rankText;
    [SerializeField] private TMP_Text _activeSlotsText;

    [Header("Tabs")]
    [SerializeField] private Button _tabAvailableButton;
    [SerializeField] private Button _tabActiveButton;

    [Header("List")]
    [SerializeField] private Transform _entryContainer;
    [SerializeField] private MissionBoardEntryUI _entryPrefab;

    [Header("Details")]
    [SerializeField] private GameObject _detailsRoot;
    [SerializeField] private TMP_Text _detailTitle;
    [SerializeField] private TMP_Text _detailRank;
    [SerializeField] private TMP_Text _detailDescription;
    [SerializeField] private Transform _objectivesContainer;
    [SerializeField] private MissionObjectiveEntryUI _objectivePrefab;
    [SerializeField] private Transform _rewardsContainer;
    [SerializeField] private MissionRewardEntryUI _rewardPrefab;
    [SerializeField] private TMP_Text _feedbackText;

    [Header("Actions")]
    [SerializeField] private Button _acceptButton;
    [SerializeField] private Button _abandonButton;
    [SerializeField] private Button _claimButton;
    [SerializeField] private Button _closeButton;

    private readonly List<GameObject> _instantiatedEntries = new();
    private readonly List<GameObject> _instantiatedObjectives = new();
    private readonly List<GameObject> _instantiatedRewards = new();

    private MissionDefinition _currentSelected;
    private GuildViewTab _activeTab = GuildViewTab.Available;

    public bool IsOpen => UiPanelVisibility.IsVisible(gameObject);
    public GuildViewTab ActiveTab => _activeTab;
    public MissionDefinition SelectedMission => _currentSelected;

    public event Action<MissionDefinition> AcceptRequested;
    public event Action<MissionDefinition> AbandonRequested;
    public event Action<MissionDefinition> ClaimRequested;
    public event Action CloseRequested;
    public event Action<MissionDefinition> MissionSelected;
    public event Action<GuildViewTab> TabChanged;

    public static TownMissionGuildView Create(Transform parent, TownMissionGuildView prefab)
    {
        if (parent == null || prefab == null)
        {
            return null;
        }

        TownMissionGuildView view = Instantiate(prefab, parent, false);
        view.name = prefab.name;

        Canvas canvas = view.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;
        }

        view.gameObject.SetActive(false);
        return view;
    }

    private void Awake()
    {
        InitializeButtons();
    }

    private void InitializeButtons()
    {
        if (_tabAvailableButton != null)
        {
            _tabAvailableButton.onClick.RemoveAllListeners();
            _tabAvailableButton.onClick.AddListener(() => SetTab(GuildViewTab.Available));
        }

        if (_tabActiveButton != null)
        {
            _tabActiveButton.onClick.RemoveAllListeners();
            _tabActiveButton.onClick.AddListener(() => SetTab(GuildViewTab.Active));
        }

        if (_acceptButton != null)
        {
            _acceptButton.onClick.RemoveAllListeners();
            _acceptButton.onClick.AddListener(() =>
            {
                if (_currentSelected != null) AcceptRequested?.Invoke(_currentSelected);
            });
        }

        if (_abandonButton != null)
        {
            _abandonButton.onClick.RemoveAllListeners();
            _abandonButton.onClick.AddListener(() =>
            {
                if (_currentSelected != null) AbandonRequested?.Invoke(_currentSelected);
            });
        }

        if (_claimButton != null)
        {
            _claimButton.onClick.RemoveAllListeners();
            _claimButton.onClick.AddListener(() =>
            {
                if (_currentSelected != null) ClaimRequested?.Invoke(_currentSelected);
            });
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
        }
    }

    public void Open()
    {
        UiPanelVisibility.SetVisible(gameObject, true);
    }

    public void Close()
    {
        UiPanelVisibility.SetVisible(gameObject, false);
    }

    public void SetTab(GuildViewTab tab)
    {
        _activeTab = tab;
        TabChanged?.Invoke(_activeTab);
    }

    public void PresentHeader(MissionRank rank, int reputation, int activeCount, int maxSlots)
    {
        if (_rankText != null)
        {
            int toNext = rank == MissionRank.E ? Mathf.Max(0, 100 - reputation) : 0;
            string nextText = toNext > 0 ? $" ({toNext} RP para siguiente rango)" : " (Rango Máximo)";
            _rankText.text = $"Gremio: Rango {rank} — {reputation} RP{nextText}";
        }

        if (_activeSlotsText != null)
        {
            _activeSlotsText.text = $"Contratos Activos: {activeCount} / {maxSlots}";
        }
    }

    public void PresentMissionList(
        IReadOnlyList<MissionDefinition> missions,
        IReadOnlyList<MissionInstanceState> activeMissions,
        MissionDefinition selected)
    {
        ClearList();

        _currentSelected = selected;
        if (_entryContainer == null) return;

        for (int i = 0; i < missions.Count; i++)
        {
            var def = missions[i];
            if (def == null) continue;

            MissionInstanceState instance = null;
            if (activeMissions != null)
            {
                for (int a = 0; a < activeMissions.Count; a++)
                {
                    if (activeMissions[a].MissionId == def.MissionId)
                    {
                        instance = activeMissions[a];
                        break;
                    }
                }
            }

            MissionState? state = instance?.State;

            if (_entryPrefab != null)
            {
                var entry = Instantiate(_entryPrefab, _entryContainer);
                entry.Initialize(def, state, OnMissionEntrySelected);
                _instantiatedEntries.Add(entry.gameObject);
            }
            else
            {
                // Fallback procedural item
                var go = new GameObject($"Entry_{def.MissionId.Value}");
                go.transform.SetParent(_entryContainer, false);
                var btn = go.AddComponent<Button>();
                var txt = go.AddComponent<TextMeshProUGUI>();
                string statusStr = state.HasValue ? $" [{state.Value}]" : "";
                txt.text = $"{def.Title}{statusStr}";
                btn.onClick.AddListener(() => OnMissionEntrySelected(def));
                _instantiatedEntries.Add(go);
            }
        }
    }

    private void OnMissionEntrySelected(MissionDefinition def)
    {
        _currentSelected = def;
        MissionSelected?.Invoke(def);
    }

    public void PresentMissionDetails(
        MissionDefinition def,
        MissionInstanceState activeInstance,
        bool canAccept,
        bool canAbandon,
        bool canClaim,
        string claimWarning = null)
    {
        _currentSelected = def;

        if (def == null)
        {
            if (_detailsRoot != null) _detailsRoot.SetActive(false);
            return;
        }

        if (_detailsRoot != null) _detailsRoot.SetActive(true);

        if (_detailTitle != null) _detailTitle.text = def.Title;
        if (_detailRank != null) _detailRank.text = $"Rango Requerido: {def.RankRequired}";
        if (_detailDescription != null) _detailDescription.text = def.Description;

        // Clear details containers
        ClearObjectivesAndRewards();

        // Populate objectives
        if (_objectivesContainer != null && def.Phases != null)
        {
            for (int phaseIdx = 0; phaseIdx < def.Phases.Count; phaseIdx++)
            {
                var phase = def.Phases[phaseIdx];
                if (phase.Objectives == null) continue;

                for (int objIdx = 0; objIdx < phase.Objectives.Count; objIdx++)
                {
                    var obj = phase.Objectives[objIdx];
                    string desc = FormatObjectiveText(phaseIdx, obj, activeInstance, objIdx);
                    bool isCompleted = IsObjectiveCompleted(phaseIdx, objIdx, obj, activeInstance);

                    if (_objectivePrefab != null)
                    {
                        var entry = Instantiate(_objectivePrefab, _objectivesContainer);
                        entry.Initialize(desc, isCompleted);
                        _instantiatedObjectives.Add(entry.gameObject);
                    }
                    else
                    {
                        var go = new GameObject($"Objective_{phaseIdx}_{objIdx}");
                        go.transform.SetParent(_objectivesContainer, false);
                        var txt = go.AddComponent<TextMeshProUGUI>();
                        txt.text = $"{(isCompleted ? "[X] " : "[ ] ")}{desc}";
                        _instantiatedObjectives.Add(go);
                    }
                }
            }
        }

        // Populate rewards
        if (_rewardsContainer != null && def.Rewards != null)
        {
            for (int i = 0; i < def.Rewards.Count; i++)
            {
                var rew = def.Rewards[i];
                string desc = $"{rew.Amount}x {rew.Type}";
                if (!string.IsNullOrEmpty(rew.ReferenceId)) desc += $" ({rew.ReferenceId})";

                if (_rewardPrefab != null)
                {
                    var entry = Instantiate(_rewardPrefab, _rewardsContainer);
                    entry.Initialize(desc, null);
                    _instantiatedRewards.Add(entry.gameObject);
                }
                else
                {
                    var go = new GameObject($"Reward_{i}");
                    go.transform.SetParent(_rewardsContainer, false);
                    var txt = go.AddComponent<TextMeshProUGUI>();
                    txt.text = desc;
                    _instantiatedRewards.Add(go);
                }
            }
        }

        // Action buttons state
        if (_acceptButton != null)
        {
            _acceptButton.gameObject.SetActive(canAccept || activeInstance == null);
            _acceptButton.interactable = canAccept;
        }

        if (_abandonButton != null)
        {
            _abandonButton.gameObject.SetActive(activeInstance != null && activeInstance.State == MissionState.Activa);
            _abandonButton.interactable = canAbandon;
        }

        if (_claimButton != null)
        {
            _claimButton.gameObject.SetActive(activeInstance != null && activeInstance.State == MissionState.PendienteDeReclamar);
            _claimButton.interactable = canClaim;
        }

        if (!string.IsNullOrEmpty(claimWarning))
        {
            ShowFeedback(claimWarning, isError: true);
        }
    }

    private static string FormatObjectiveText(int phaseIndex, ObjectiveDefinition obj, MissionInstanceState instance, int objIndex)
    {
        string baseText = $"Fase {phaseIndex + 1}: {obj.Family} {obj.Condition.RequiredAmount}x {obj.Condition.TargetId}";
        if (instance != null && instance.CurrentPhaseIndex == phaseIndex && instance.ObjectiveProgress != null)
        {
            if (instance.ObjectiveProgress.TryGetValue(objIndex, out var prog))
            {
                baseText += $" ({prog.CurrentAmount}/{obj.Condition.RequiredAmount})";
            }
        }
        return baseText;
    }

    private static bool IsObjectiveCompleted(int phaseIndex, int objIndex, ObjectiveDefinition obj, MissionInstanceState instance)
    {
        if (instance == null) return false;
        if (instance.State == MissionState.PendienteDeReclamar || instance.State == MissionState.Reclamada) return true;
        if (phaseIndex < instance.CurrentPhaseIndex) return true;
        if (phaseIndex == instance.CurrentPhaseIndex && instance.ObjectiveProgress != null)
        {
            if (instance.ObjectiveProgress.TryGetValue(objIndex, out var prog))
            {
                return prog.CurrentAmount >= obj.Condition.RequiredAmount;
            }
        }
        return false;
    }

    public void ShowFeedback(string text, bool isError = false)
    {
        if (_feedbackText != null)
        {
            _feedbackText.text = text ?? string.Empty;
            _feedbackText.color = isError ? new Color(1f, 0.4f, 0.4f, 1f) : new Color(0.4f, 1f, 0.4f, 1f);
        }
    }

    private void ClearList()
    {
        for (int i = 0; i < _instantiatedEntries.Count; i++)
        {
            if (_instantiatedEntries[i] != null) Destroy(_instantiatedEntries[i]);
        }
        _instantiatedEntries.Clear();
    }

    private void ClearObjectivesAndRewards()
    {
        for (int i = 0; i < _instantiatedObjectives.Count; i++)
        {
            if (_instantiatedObjectives[i] != null) Destroy(_instantiatedObjectives[i]);
        }
        _instantiatedObjectives.Clear();

        for (int i = 0; i < _instantiatedRewards.Count; i++)
        {
            if (_instantiatedRewards[i] != null) Destroy(_instantiatedRewards[i]);
        }
        _instantiatedRewards.Clear();
    }

    private void OnDestroy()
    {
        ClearList();
        ClearObjectivesAndRewards();
    }
}
