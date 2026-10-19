using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds <see cref="UiButtonFeedback"/> to every Button and Toggle authored in UI prefabs and in the
/// open scenes. Idempotent: controls that already have it are skipped. Nested prefab instances are
/// left to their own source prefab so no instance overrides are created.
/// </summary>
public static class UiButtonFeedbackInstaller
{
    private const string PrefabRoot = "Assets/Prefabs";

    [MenuItem("Grimhold/UI/Add Button Feedback To Prefabs And Open Scenes")]
    public static void InstallFromMenu()
    {
        int added = Install();
        Debug.Log($"[{nameof(UiButtonFeedbackInstaller)}] Added {added} {nameof(UiButtonFeedback)} component(s).");
    }

    public static int Install()
    {
        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int prefabAdded = AddToHierarchy(contents);
                if (prefabAdded > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    added += prefabAdded;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (!scene.isLoaded)
            {
                continue;
            }

            int sceneAdded = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                sceneAdded += AddToHierarchy(root);
            }

            if (sceneAdded > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                added += sceneAdded;
            }
        }

        return added;
    }

    private static int AddToHierarchy(GameObject root)
    {
        var controls = new List<Selectable>();
        root.GetComponentsInChildren(true, controls);
        int added = 0;
        foreach (Selectable control in controls)
        {
            if (control is not Button && control is not Toggle)
            {
                continue;
            }

            GameObject target = control.gameObject;
            // Opened prefab contents and scene objects are not instances themselves; anything that is
            // belongs to a nested prefab (or a variant's base) and is handled in that source asset.
            if (target.GetComponent<UiButtonFeedback>() != null || PrefabUtility.IsPartOfPrefabInstance(target))
            {
                continue;
            }

            Undo.AddComponent<UiButtonFeedback>(target);
            added++;
        }

        return added;
    }
}
