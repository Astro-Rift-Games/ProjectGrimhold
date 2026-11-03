using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Subscribes to local profile mission progress updates during a raid
/// and presents active contracts on the <see cref="RaidMissionTrackerView"/>.
/// Immediately and reactively updates without polling.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidMissionTrackerPresenter : MonoBehaviour
{
    [SerializeField] private RaidMissionTrackerView _view;
    [SerializeField] private MissionDefinitionCatalog _catalog;

    private LocalProfileStore _store;
    private bool _isBound;

    public RaidMissionTrackerView View => _view;
    public bool IsBound => _isBound;

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

        Refresh();
    }

    public void Unbind()
    {
        if (_store != null)
        {
            _store.ProfileCommitted -= OnProfileCommitted;
            _store.MissionProgressChanged -= OnMissionProgressChanged;
        }

        _store = null;
        _isBound = false;

        if (_view != null)
        {
            _view.Present(Array.Empty<MissionTrackerItemViewModel>());
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
        Refresh();
        _view?.PulseMission(missionId, state == MissionState.PendienteDeReclamar);
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
