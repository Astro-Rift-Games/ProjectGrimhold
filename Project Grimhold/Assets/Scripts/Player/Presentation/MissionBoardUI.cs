using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller for the Mission Board UI.
/// Reads mission definitions from the catalog and interacts with the LocalProfileStore
/// to display available missions and accept them.
/// </summary>
[DisallowMultipleComponent]
public sealed class MissionBoardUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private MissionBoardEntryUI _entryPrefab;
    [SerializeField] private Transform _missionListContainer;
    [SerializeField] private Button _closeButton;

    [Header("Mission Details Panel")]
    [SerializeField] private GameObject _detailsPanelRoot;
    [SerializeField] private TMP_Text _detailTitle;
    [SerializeField] private TMP_Text _detailDescription;
    [SerializeField] private Transform _objectivesContainer;
    [SerializeField] private MissionObjectiveEntryUI _objectivePrefab;
    [SerializeField] private Transform _rewardsContainer;
    [SerializeField] private MissionRewardEntryUI _rewardPrefab;
    [SerializeField] private Button _acceptButton;
    [SerializeField] private TMP_Text _acceptButtonText;

    [Header("Input / Testing")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.M;
    [SerializeField] private GameObject _visualRoot;

    private Canvas _canvas;
    private ApplicationStashContext _context;
    private readonly List<MissionBoardEntryUI> _instantiatedEntries = new();
    private readonly List<MissionObjectiveEntryUI> _instantiatedObjectives = new();
    private readonly List<MissionRewardEntryUI> _instantiatedRewards = new();
    private MissionDefinition _selectedMission;

    public bool IsOpen
    {
        get
        {
            if (_visualRoot != null) return _visualRoot.activeSelf;
            if (_canvas != null) return _canvas.enabled;
            return gameObject.activeSelf;
        }
    }

    public void Open()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            return;
        }

        if (_visualRoot != null) _visualRoot.SetActive(true);
        if (_canvas != null) _canvas.enabled = true;

        RefreshState();
    }

    public void Close()
    {
        if (_visualRoot != null)
        {
            _visualRoot.SetActive(false);
        }
        else if (_canvas != null)
        {
            _canvas.enabled = false;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _context = FindAnyObjectByType<ApplicationStashContext>();
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Close);
        }

        if (_acceptButton != null)
        {
            _acceptButton.onClick.AddListener(OnAcceptMissionClicked);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey) || (IsOpen && Input.GetKeyDown(KeyCode.Escape)))
        {
            Toggle();
        }
    }

    private void OnEnable()
    {
        RefreshState();
    }

    private void RefreshState()
    {
        if (_context == null)
        {
            _context = FindAnyObjectByType<ApplicationStashContext>();
        }

        if (_context == null || _context.Store == null || _context.Store.MissionCatalog == null)
        {
            return;
        }

        _context.Store.ProfileCommitted -= OnProfileCommitted;
        _context.Store.ProfileCommitted += OnProfileCommitted;

        RefreshMissionList();
        SelectMission(null); // Clear selection
    }

    private void OnDisable()
    {
        if (_context != null && _context.Store != null)
        {
            _context.Store.ProfileCommitted -= OnProfileCommitted;
        }
    }

    private void OnProfileCommitted(ProfileId profileId)
    {
        // Re-evaluate list and current selection if profile changes (e.g. accepted a mission)
        if (gameObject.activeInHierarchy)
        {
            RefreshMissionList();
            if (_selectedMission != null)
            {
                SelectMission(_selectedMission);
            }
        }
    }

    private void RefreshMissionList()
    {
        // Clear previous entries
        foreach (var entry in _instantiatedEntries)
        {
            if (entry != null) Destroy(entry.gameObject);
        }
        _instantiatedEntries.Clear();

        if (_entryPrefab == null || _missionListContainer == null)
        {
            return;
        }

        var catalog = _context.Store.MissionCatalog;
        var activeMissions = _context.Store.GetActiveMissions();

        for (int i = 0; i < catalog.DefinitionCount; i++)
        {
            if (catalog.TryGetByIndex(i, out var def))
            {
                MissionState? currentState = null;
                var instance = activeMissions.FirstOrDefault(m => m.MissionId == def.MissionId);
                if (instance != null)
                {
                    currentState = instance.State;
                }

                var entryGo = Instantiate(_entryPrefab, _missionListContainer);
                entryGo.Initialize(def, currentState, SelectMission);
                _instantiatedEntries.Add(entryGo);
            }
        }
    }

    private void SelectMission(MissionDefinition def)
    {
        _selectedMission = def;

        if (def == null)
        {
            if (_detailsPanelRoot != null) _detailsPanelRoot.SetActive(false);
            return;
        }

        if (_detailsPanelRoot != null) _detailsPanelRoot.SetActive(true);

        if (_detailTitle != null) _detailTitle.text = def.Title;
        if (_detailDescription != null) _detailDescription.text = def.Description;

        // Clear previous objective and reward instances
        foreach (var obj in _instantiatedObjectives) { if (obj != null) Destroy(obj.gameObject); }
        _instantiatedObjectives.Clear();
        foreach (var rew in _instantiatedRewards) { if (rew != null) Destroy(rew.gameObject); }
        _instantiatedRewards.Clear();

        // Evaluate Accept button state and get active instance state
        var activeMissions = _context.Store.GetActiveMissions();
        var instance = activeMissions.FirstOrDefault(m => m.MissionId == def.MissionId);

        // Build Objectives
        if (_objectivesContainer != null && _objectivePrefab != null)
        {
            if (def.Phases != null)
            {
                for (int i = 0; i < def.Phases.Count; i++)
                {
                    var phase = def.Phases[i];
                    if (phase.Objectives != null)
                    {
                        for (int objIndex = 0; objIndex < phase.Objectives.Count; objIndex++)
                        {
                            var obj = phase.Objectives[objIndex];
                            var entryGo = Instantiate(_objectivePrefab, _objectivesContainer);
                            
                            string desc = $"Fase {i + 1}: {obj.Family} {obj.Condition.RequiredAmount}x {obj.Condition.TargetId}";
                            
                            bool isCompleted = false;
                            if (instance != null)
                            {
                                if (instance.State == MissionState.Reclamada || instance.State == MissionState.PendienteDeReclamar)
                                {
                                    isCompleted = true;
                                }
                                else if (i < instance.CurrentPhaseIndex)
                                {
                                    isCompleted = true;
                                }
                                else if (i == instance.CurrentPhaseIndex)
                                {
                                    if (instance.ObjectiveProgress.TryGetValue(objIndex, out var prog))
                                    {
                                        isCompleted = prog.CurrentAmount >= obj.Condition.RequiredAmount;
                                        desc += $" ({prog.CurrentAmount}/{obj.Condition.RequiredAmount})";
                                    }
                                }
                            }

                            entryGo.Initialize(desc, isCompleted);
                            _instantiatedObjectives.Add(entryGo);
                        }
                    }
                }
            }
        }

        // Build Rewards
        if (_rewardsContainer != null && _rewardPrefab != null)
        {
            if (def.Rewards != null)
            {
                foreach (var rew in def.Rewards)
                {
                    var entryGo = Instantiate(_rewardPrefab, _rewardsContainer);
                    string desc = $"{rew.Amount}x {rew.Type}";
                    if (!string.IsNullOrEmpty(rew.ReferenceId)) desc += $" ({rew.ReferenceId})";
                    entryGo.Initialize(desc, null);
                    _instantiatedRewards.Add(entryGo);
                }
            }
        }

        if (_acceptButton != null)
        {
            if (instance != null)
            {
                // Already accepted or completed
                _acceptButton.interactable = false;
                if (_acceptButtonText != null)
                {
                    _acceptButtonText.text = instance.State == MissionState.Reclamada ? "Completada" : "Ya Aceptada";
                }
            }
            else
            {
                // Available
                _acceptButton.interactable = true;
                if (_acceptButtonText != null) _acceptButtonText.text = "Aceptar Contrato";
            }
        }
    }

    private void OnAcceptMissionClicked()
    {
        if (_selectedMission == null || _context == null || _context.Store == null) return;

        var result = _context.Store.TryAcceptMission(_selectedMission);
        if (result != StashOperationResult.Success)
        {
            Debug.LogError($"[MissionBoardUI] Error al aceptar misión '{_selectedMission.Title}': {result}");
        }
    }
}
