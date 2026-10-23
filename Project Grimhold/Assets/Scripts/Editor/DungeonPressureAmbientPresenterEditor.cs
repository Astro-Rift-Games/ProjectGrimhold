#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for <see cref="DungeonPressureAmbientPresenter"/> providing one-click
/// Edit Mode preview buttons to calibrate global lights, prop lights, and post-process vignette
/// without having to enter Play Mode.
/// </summary>
[CustomEditor(typeof(DungeonPressureAmbientPresenter))]
public sealed class DungeonPressureAmbientPresenterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var presenter = (DungeonPressureAmbientPresenter)target;

        if (Application.isPlaying || presenter.Config == null)
        {
            return;
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Edit Mode Phase Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Click a phase button below to preview global ambient light, torch props, and post-process vignette in the Scene and Game views outside Play Mode.",
            MessageType.Info);

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Normal", GUILayout.Height(28)))
        {
            presenter.PreviewPhase(DungeonPressurePhase.Normal);
        }
        if (GUILayout.Button("Reinforcements", GUILayout.Height(28)))
        {
            presenter.PreviewPhase(DungeonPressurePhase.Reinforcements);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Critical Pressure", GUILayout.Height(28)))
        {
            presenter.PreviewPhase(DungeonPressurePhase.CriticalPressure);
        }
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("Collapse", GUILayout.Height(28)))
        {
            presenter.PreviewPhase(DungeonPressurePhase.Collapse);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Clear / Reset Preview", GUILayout.Height(24)))
        {
            presenter.ClearPreview();
        }
    }
}
#endif
