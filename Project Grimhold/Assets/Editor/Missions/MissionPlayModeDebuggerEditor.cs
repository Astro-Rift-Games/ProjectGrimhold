using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MissionPlayModeDebugger))]
public class MissionPlayModeDebuggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var debugger = (MissionPlayModeDebugger)target;
        var context = FindAnyObjectByType<ApplicationStashContext>();
        var store = context != null ? context.Store : null;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("--- PlayMode Mission Controls ---", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Entra a PlayMode para interactuar con el Store de perfiles y ver el progreso.", MessageType.Info);
            return;
        }

        if (store == null || !store.IsAvailable)
        {
            EditorGUILayout.HelpBox("Esperando que el LocalProfileStore esté listo...", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox($"Perfil Activo: {store.ProfileId.Value}", MessageType.None);

        if (debugger.MissionToAccept != null)
        {
            if (GUILayout.Button($"Aceptar Misión Seleccionada ({debugger.MissionToAccept.Id})", GUILayout.Height(30)))
            {
                debugger.AcceptMission();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Arrastra una MissionDefinition a 'Mission To Accept' para poder aceptarla con un click.", MessageType.None);
        }

        if (GUILayout.Button("Imprimir Misiones Activas en Consola"))
        {
            debugger.LogActiveMissions();
        }

        var activeList = store.GetActiveMissions();
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField($"Misiones Activas ({activeList.Count})", EditorStyles.boldLabel);

        if (activeList.Count == 0)
        {
            EditorGUILayout.LabelField("No hay misiones activas.");
            return;
        }

        for (int i = 0; i < activeList.Count; i++)
        {
            var instance = activeList[i];
            var def = debugger.FindDefinition(instance.MissionId.Value);
            string title = def != null ? def.Title : instance.MissionId.Value;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"<b>{title}</b> - Estado: <i>{instance.State}</i>", new GUIStyle(EditorStyles.label) { richText = true });

            if (def != null && instance.CurrentPhaseIndex < def.Phases.Count)
            {
                EditorGUILayout.LabelField($"Fase {instance.CurrentPhaseIndex + 1} de {def.Phases.Count}");
                var phase = def.Phases[instance.CurrentPhaseIndex];

                for (int o = 0; o < phase.Objectives.Count; o++)
                {
                    var obj = phase.Objectives[o];
                    int current = 0;
                    if (instance.ObjectiveProgress.TryGetValue(o, out var pState))
                    {
                        current = pState.CurrentAmount;
                    }
                    int req = obj.Condition.RequiredAmount;
                    float progress = Mathf.Clamp01((float)current / req);

                    string targetText = !string.IsNullOrEmpty(obj.Condition.TargetId) ? $" [{obj.Condition.TargetId}]" : "";
                    string label = $"{obj.Family}{targetText}: {current}/{req}";
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 18), progress, label);
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (instance.State == MissionState.Activa)
            {
                if (GUILayout.Button("Abandonar", GUILayout.Width(100)))
                {
                    store.TryAbandonMission(instance.MissionId);
                }
            }
            if (instance.State == MissionState.Completada && def != null)
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Reclamar Recompensa", GUILayout.Width(160)))
                {
                    store.TryClaimMission(def);
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        Repaint();
    }
}
