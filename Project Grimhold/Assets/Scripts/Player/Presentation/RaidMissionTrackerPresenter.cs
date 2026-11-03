using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Subscribes to local profile mission progress updates during a raid
/// and presents active contracts on the <see cref="RaidMissionTrackerView"/>.
/// Immediately and reactively updates without polling, manages hotkey toggle,
/// and displays HUD popups when objectives advance or complete.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidMissionTrackerPresenter : MonoBehaviour
{
    [SerializeField] private RaidMissionTrackerView _view;
    [SerializeField] private MissionDefinitionCatalog _catalog;
    [SerializeField] private KeyCode _toggleKey = KeyCode.J;
    [SerializeField] private KeyCode _altToggleKey = KeyCode.O;

    private LocalProfileStore _store;
    private bool _isBound;

    private readonly Dictionary<MissionId, MissionProgressSnapshot> _snapshots = new();

    private sealed class MissionProgressSnapshot
    {
        public MissionState State;
        public int PhaseIndex;
        public readonly Dictionary<int, int> ObjectiveAmounts = new();
    }

    public RaidMissionTrackerView View => _view;
    public bool IsBound => _isBound;
    public KeyCode ToggleKey { get => _toggleKey; set => _toggleKey = value; }

    public void Bind(LocalProfileStore store, MissionDefinitionCatalog catalog = null, RaidMissionTrackerView view = null)
    {
        Unbind();

        _store = store ?? throw new ArgumentNullException(nameof(store));
        if (catalog != null) _catalog = catalog;
        if (view != null) _view = view;

        if (_catalog == null)
        {
            _catalog = _store.MissionCatalog;
        }

        if (_view == null)
        {
            _view = GetComponentInChildren<RaidMissionTrackerView>(true);
        }

        _store.ProfileCommitted += OnProfileCommitted;
        _store.MissionProgressChanged += OnMissionProgressChanged;
        _isBound = true;

        CaptureInitialSnapshots();
        Refresh();
    }

    public void Unbind()
    {
        if (_store != null)
        {
            _store.ProfileCommitted -= OnProfileCommitted;
            _store.MissionProgressChanged -= OnMissionProgressChanged;
        }

        _snapshots.Clear();
        _store = null;
        _isBound = false;

        if (_view != null)
        {
            _view.Present(Array.Empty<MissionTrackerItemViewModel>());
        }
    }

    private void Update()
    {
        if (!_isBound || _view == null) return;

        bool togglePressed = false;

        if (Input.GetKeyDown(_toggleKey) || Input.GetKeyDown(_altToggleKey) || Input.GetKeyDown(KeyCode.L))
        {
            togglePressed = true;
        }
#if ENABLE_INPUT_SYSTEM
        else if (Keyboard.current != null)
        {
            var kb = Keyboard.current;
            if (kb.jKey.wasPressedThisFrame || kb.oKey.wasPressedThisFrame || kb.lKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
        }
#endif

        if (togglePressed)
        {
            _view.ToggleCollapse();
        }
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void OnProfileCommitted(ProfileId profileId)
    {
        if (!_isBound || _store == null) return;
        Refresh();
    }

    private void OnMissionProgressChanged(MissionId missionId, MissionState state)
    {
        if (!_isBound || _store == null) return;

        var catalog = _catalog != null ? _catalog : _store.MissionCatalog;
        MissionDefinition def = null;
        if (catalog != null)
        {
            catalog.TryGet(missionId.Value, out def);
        }

        string title = def != null ? def.Title : missionId.Value;

        var activeInstances = _store.GetActiveMissions();
        MissionInstanceState currentInstance = default;
        bool foundInstance = false;
        for (int i = 0; i < activeInstances.Count; i++)
        {
            if (activeInstances[i].MissionId == missionId)
            {
                currentInstance = activeInstances[i];
                foundInstance = true;
                break;
            }
        }

        _snapshots.TryGetValue(missionId, out var previousSnapshot);

        if (state == MissionState.PendienteDeReclamar)
        {
            if (previousSnapshot == null || previousSnapshot.State != MissionState.PendienteDeReclamar)
            {
                _view?.ShowProgressPopup("¡CONTRATO COMPLETADO!", title, "¡Objetivos completados! Reclama en la Guild de Incursiones.", true);
            }
        }
        else if (state == MissionState.Activa && foundInstance && def != null && def.Phases != null)
        {
            if (currentInstance.CurrentPhaseIndex >= 0 && currentInstance.CurrentPhaseIndex < def.Phases.Count)
            {
                var phase = def.Phases[currentInstance.CurrentPhaseIndex];
                if (phase.Objectives != null && currentInstance.ObjectiveProgress != null)
                {
                    for (int o = 0; o < phase.Objectives.Count; o++)
                    {
                        var objDef = phase.Objectives[o];
                        int currentAmount = currentInstance.ObjectiveProgress.TryGetValue(o, out var prog) ? prog.CurrentAmount : 0;
                        int prevAmount = 0;
                        if (previousSnapshot != null && previousSnapshot.ObjectiveAmounts.TryGetValue(o, out var p))
                        {
                            prevAmount = p;
                        }

                        if (currentAmount > prevAmount)
                        {
                            int required = objDef.Condition.RequiredAmount;
                            string desc = FormatObjectiveSummary(objDef);
                            _view?.ShowProgressPopup("PROGRESO DE CONTRATO", title, $"{desc}: {currentAmount}/{required}", false);
                            break;
                        }
                    }
                }
            }
        }

        UpdateSnapshot(missionId, foundInstance ? currentInstance : new MissionInstanceState(missionId, state));
        Refresh();
        _view?.PulseMission(missionId, state == MissionState.PendienteDeReclamar);
    }

    private void CaptureInitialSnapshots()
    {
        _snapshots.Clear();
        if (_store == null) return;

        var active = _store.GetActiveMissions();
        for (int i = 0; i < active.Count; i++)
        {
            UpdateSnapshot(active[i].MissionId, active[i]);
        }
    }

    private void UpdateSnapshot(MissionId missionId, MissionInstanceState instance)
    {
        if (!_snapshots.TryGetValue(missionId, out var snapshot))
        {
            snapshot = new MissionProgressSnapshot();
            _snapshots[missionId] = snapshot;
        }

        snapshot.State = instance.State;
        snapshot.PhaseIndex = instance.CurrentPhaseIndex;
        snapshot.ObjectiveAmounts.Clear();
        if (instance.ObjectiveProgress != null)
        {
            foreach (var kvp in instance.ObjectiveProgress)
            {
                snapshot.ObjectiveAmounts[kvp.Key] = kvp.Value.CurrentAmount;
            }
        }
    }

    public void Refresh()
    {
        if (!_isBound || _store == null || _view == null) return;

        var catalog = _catalog != null ? _catalog : _store.MissionCatalog;
        if (catalog == null) return;

        var activeInstances = _store.GetActiveMissions();
        var items = new List<MissionTrackerItemViewModel>();

        for (int i = 0; i < activeInstances.Count; i++)
        {
            var instance = activeInstances[i];
            if (instance.State != MissionState.Activa && instance.State != MissionState.PendienteDeReclamar)
            {
                continue;
            }

            if (!catalog.TryGet(instance.MissionId.Value, out var def) || def == null)
            {
                continue;
            }

            var objectives = new List<MissionTrackerObjectiveViewModel>();
            if (def.Phases != null && instance.CurrentPhaseIndex >= 0 && instance.CurrentPhaseIndex < def.Phases.Count)
            {
                var phase = def.Phases[instance.CurrentPhaseIndex];
                if (phase.Objectives != null)
                {
                    for (int o = 0; o < phase.Objectives.Count; o++)
                    {
                        var objDef = phase.Objectives[o];
                        int current = 0;
                        if (instance.ObjectiveProgress != null && instance.ObjectiveProgress.TryGetValue(o, out var prog))
                        {
                            current = prog.CurrentAmount;
                        }

                        int required = objDef.Condition.RequiredAmount;
                        bool completed = current >= required || instance.State == MissionState.PendienteDeReclamar;
                        string desc = FormatObjectiveSummary(objDef);

                        objectives.Add(new MissionTrackerObjectiveViewModel(desc, current, required, completed));
                    }
                }
            }

            items.Add(new MissionTrackerItemViewModel(instance.MissionId, def.Title, instance.State, objectives));
        }

        _view.Present(items);
    }

    private static string FormatObjectiveSummary(ObjectiveDefinition objDef)
    {
        switch (objDef.Family)
        {
            case ObjectiveFamily.EliminacionPvE:
                return "Eliminar enemigos";
            case ObjectiveFamily.EliminacionEspecifica:
                return string.IsNullOrEmpty(objDef.Condition.TargetId) ? "Eliminar objetivo" : $"Eliminar {objDef.Condition.TargetId}";
            case ObjectiveFamily.Exploracion:
                return string.IsNullOrEmpty(objDef.Condition.ZoneId) ? "Explorar zona" : $"Explorar {objDef.Condition.ZoneId}";
            case ObjectiveFamily.Interaccion:
                return "Saquear cofres";
            case ObjectiveFamily.Obtencion:
            case ObjectiveFamily.ExtraccionDeObjetos:
                return string.IsNullOrEmpty(objDef.Condition.TargetId) ? "Extraer objetos" : $"Extraer {objDef.Condition.TargetId}";
            case ObjectiveFamily.PvP:
                return "Bajas PvP";
            default:
                return objDef.Family.ToString();
        }
    }
}
