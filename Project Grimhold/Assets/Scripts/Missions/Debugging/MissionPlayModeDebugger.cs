using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime developer/testing tool for testing missions during PlayMode.
/// Allows accepting missions, tracking real-time objective progress, claiming rewards,
/// and simulating contribution events without requiring a full UI frontend.
/// </summary>
[DisallowMultipleComponent]
public sealed class MissionPlayModeDebugger : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Optional. Specific mission asset to accept or test.")]
    public MissionDefinition MissionToAccept;

    [Tooltip("Optional. Catalog containing all available missions.")]
    public MissionDefinitionCatalog Catalog;

    [Header("IMGUI Overlay Settings")]
    [SerializeField] private bool _showOverlay = true;
    [SerializeField] private KeyCode _toggleKey = KeyCode.F7;

    private ApplicationStashContext _context;
    private Rect _windowRect = new(12f, 220f, 480f, 380f);
    private Vector2 _scrollPosition;
    private string _simulateTargetId = "";
    private int _simulateAmount = 1;

    private LocalProfileStore Store => _context != null ? _context.Store : null;

    private void Awake()
    {
        _context = FindAnyObjectByType<ApplicationStashContext>();
        if (Catalog == null)
        {
            var config = Resources.Load<LocalProfilePersistenceConfiguration>("LocalProfilePersistenceConfiguration");
            if (config != null)
            {
                Catalog = config.MissionCatalog;
            }
        }
    }

    private void Update()
    {
        if (_context == null)
        {
            _context = FindAnyObjectByType<ApplicationStashContext>();
        }

        if (Input.GetKeyDown(_toggleKey))
        {
            _showOverlay = !_showOverlay;
        }
    }

    [ContextMenu("Accept Configured Mission")]
    public void AcceptMission()
    {
        if (Store == null || !Store.IsAvailable)
        {
            Debug.LogError("[MissionDebugger] LocalProfileStore is not available yet.");
            return;
        }

        if (MissionToAccept == null)
        {
            Debug.LogWarning("[MissionDebugger] No MissionToAccept specified in Inspector.");
            return;
        }

        var result = Store.TryAcceptMission(MissionToAccept);
        Debug.Log($"[MissionDebugger] TryAcceptMission('{MissionToAccept.Id}') result: {result}");
    }

    [ContextMenu("Log Active Missions Progress")]
    public void LogActiveMissions()
    {
        if (Store == null || !Store.IsAvailable)
        {
            Debug.LogWarning("[MissionDebugger] LocalProfileStore is not available yet.");
            return;
        }

        var activeList = Store.GetActiveMissions();
        if (activeList == null || activeList.Count == 0)
        {
            Debug.Log("[MissionDebugger] No active missions found in current profile.");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[MissionDebugger] Active Missions count: {activeList.Count}");

        for (int i = 0; i < activeList.Count; i++)
        {
            var instance = activeList[i];
            sb.AppendLine($"--- Mission #{i + 1}: ID='{instance.MissionId.Value}', State={instance.State}, PhaseIndex={instance.CurrentPhaseIndex} ---");

            MissionDefinition definition = FindDefinition(instance.MissionId.Value);
            if (definition == null)
            {
                sb.AppendLine("    (Definition not found in Catalog or MissionToAccept)");
                continue;
            }

            if (instance.CurrentPhaseIndex < 0 || instance.CurrentPhaseIndex >= definition.Phases.Count)
            {
                sb.AppendLine("    (PhaseIndex out of range)");
                continue;
            }

            var phase = definition.Phases[instance.CurrentPhaseIndex];
            for (int objIdx = 0; objIdx < phase.Objectives.Count; objIdx++)
            {
                var objDef = phase.Objectives[objIdx];
                int current = 0;
                if (instance.ObjectiveProgress.TryGetValue(objIdx, out var progressState))
                {
                    current = progressState.CurrentAmount;
                }
                int required = objDef.Condition.RequiredAmount;
                bool done = current >= required;

                sb.AppendLine($"    [Obj {objIdx}] Family={objDef.Family}, Target='{objDef.Condition.TargetId}', Zone='{objDef.Condition.ZoneId}': {current}/{required} {(done ? "[COMPLETED]" : "[IN PROGRESS]")}");
            }
        }

        Debug.Log(sb.ToString());
    }

    [ContextMenu("Abandon Configured Mission")]
    public void AbandonMission()
    {
        if (Store == null || !Store.IsAvailable || MissionToAccept == null) return;
        var result = Store.TryAbandonMission(MissionToAccept.MissionId);
        Debug.Log($"[MissionDebugger] TryAbandonMission('{MissionToAccept.Id}') result: {result}");
    }

    [ContextMenu("Claim Configured Mission")]
    public void ClaimMission()
    {
        if (Store == null || !Store.IsAvailable || MissionToAccept == null) return;
        var result = Store.TryClaimMission(MissionToAccept);
        Debug.Log($"[MissionDebugger] TryClaimMission('{MissionToAccept.Id}') result: {result}");
    }

    public MissionDefinition FindDefinition(string missionId)
    {
        if (MissionToAccept != null && MissionToAccept.Id == missionId)
        {
            return MissionToAccept;
        }

        if (Catalog != null && Catalog.TryGet(missionId, out var def))
        {
            return def;
        }

        return null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (Store == null || !Store.IsAvailable)
        {
            GUI.Label(new Rect(12f, 180f, 300f, 24f), "[Missions] Waiting for Profile Store...");
            return;
        }

        var activeMissions = Store.GetActiveMissions();
        int activeCount = activeMissions != null ? activeMissions.Count : 0;

        if (!_showOverlay)
        {
            if (GUI.Button(new Rect(12f, 180f, 200f, 26f), $"Missions Debug ({activeCount} Active) [{_toggleKey}]"))
            {
                _showOverlay = true;
            }
            return;
        }

        _windowRect = GUI.Window(888123, _windowRect, DrawWindow, $"Missions PlayMode Debugger ({activeCount} Active)");
        _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
        _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Profile: {Store.ProfileId.Value.Substring(0, Math.Min(8, Store.ProfileId.Value.Length))}...", GUILayout.Width(150));
        if (GUILayout.Button($"Hide [{_toggleKey}]", GUILayout.Width(90)))
        {
            _showOverlay = false;
        }
        GUILayout.EndHorizontal();

        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        // Section 1: Accept Configured Mission
        GUILayout.Space(6);
        GUILayout.Label("--- Aceptar Misión de Prueba ---", GUI.skin.box);
        if (MissionToAccept != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Slot: <b>{MissionToAccept.Title}</b> ({MissionToAccept.Id})", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Aceptar", GUILayout.Width(80)))
            {
                var res = Store.TryAcceptMission(MissionToAccept);
                Debug.Log($"[MissionDebugger] Aceptada: {res}");
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("Arrastra una MissionDefinition al campo 'MissionToAccept' en el Inspector.");
        }

        // Section 2: Catalog missions quick accept
        if (Catalog != null && Catalog.DefinitionCount > 0)
        {
            GUILayout.Space(6);
            GUILayout.Label("--- Misiones en Catálogo ---", GUI.skin.box);
            for (int idx = 0; idx < Catalog.DefinitionCount; idx++)
            {
                if (!Catalog.TryGetByIndex(idx, out var def) || def == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{def.Title} ({def.Id})", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Aceptar", GUILayout.Width(70)))
                {
                    var res = Store.TryAcceptMission(def);
                    Debug.Log($"[MissionDebugger] Aceptar '{def.Id}': {res}");
                }
                GUILayout.EndHorizontal();
            }
        }

        // Section 3: Active missions & progress
        GUILayout.Space(8);
        GUILayout.Label("--- Misiones Activas y Progreso ---", GUI.skin.box);
        var activeMissions = Store.GetActiveMissions();
        if (activeMissions == null || activeMissions.Count == 0)
        {
            GUILayout.Label("No hay misiones activas en este momento.");
        }
        else
        {
            for (int i = 0; i < activeMissions.Count; i++)
            {
                var instance = activeMissions[i];
                var def = FindDefinition(instance.MissionId.Value);
                string title = def != null ? def.Title : instance.MissionId.Value;

                GUILayout.BeginVertical(GUI.skin.textArea);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{title}</b>", GUILayout.ExpandWidth(true));
                GUILayout.Label($"[{instance.State}]", GUILayout.Width(90));
                GUILayout.EndHorizontal();

                if (def != null && instance.CurrentPhaseIndex < def.Phases.Count)
                {
                    GUILayout.Label($"Fase: {instance.CurrentPhaseIndex + 1} / {def.Phases.Count}");
                    var phase = def.Phases[instance.CurrentPhaseIndex];
                    for (int o = 0; o < phase.Objectives.Count; o++)
                    {
                        var obj = phase.Objectives[o];
                        int curr = 0;
                        if (instance.ObjectiveProgress.TryGetValue(o, out var pState))
                        {
                            curr = pState.CurrentAmount;
                        }
                        int req = obj.Condition.RequiredAmount;
                        bool done = curr >= req;

                        string mark = done ? "<b>[✓ COMPLETADO]</b>" : "[EN PROGRESO]";
                        string target = !string.IsNullOrEmpty(obj.Condition.TargetId) ? $" (Target: {obj.Condition.TargetId})" : "";
                        GUILayout.Label($"  • [{obj.Family}]{target}: {curr} / {req} {mark}");
                    }
                }

                GUILayout.BeginHorizontal();
                if (instance.State == MissionState.Activa)
                {
                    if (GUILayout.Button("Abandonar", GUILayout.Width(90)))
                    {
                        Store.TryAbandonMission(instance.MissionId);
                    }
                }
                if (instance.State == MissionState.Completada && def != null)
                {
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("Reclamar Recompensa", GUILayout.Width(160)))
                    {
                        Store.TryClaimMission(def);
                    }
                    GUI.backgroundColor = Color.white;
                }
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                GUILayout.Space(4);
            }
        }

        // Section 4: Event Simulation
        GUILayout.Space(8);
        GUILayout.Label("--- Simulación Rápida de Progreso ---", GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label("TargetId:", GUILayout.Width(65));
        _simulateTargetId = GUILayout.TextField(_simulateTargetId, GUILayout.Width(120));
        GUILayout.Label("Cant:", GUILayout.Width(40));
        string amtStr = GUILayout.TextField(_simulateAmount.ToString(), GUILayout.Width(40));
        int.TryParse(amtStr, out _simulateAmount);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Simular Kill (Eliminación PvE)"))
        {
            var ev = new MissionContributionEvent(
                ObjectiveFamily.EliminacionPvE,
                Mathf.Max(1, _simulateAmount),
                Store.ProfileId,
                _simulateTargetId,
                "",
                false);
            var res = Store.TryApplyMissionProgress(ev);
            Debug.Log($"[MissionDebugger] Simular Kill: {res}");
        }

        if (GUILayout.Button("Simular Cofre (Interacción)"))
        {
            var ev = new MissionContributionEvent(
                ObjectiveFamily.Interaccion,
                Mathf.Max(1, _simulateAmount),
                Store.ProfileId,
                _simulateTargetId,
                "",
                false);
            var res = Store.TryApplyMissionProgress(ev);
            Debug.Log($"[MissionDebugger] Simular Interacción: {res}");
        }
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }
#endif
}
