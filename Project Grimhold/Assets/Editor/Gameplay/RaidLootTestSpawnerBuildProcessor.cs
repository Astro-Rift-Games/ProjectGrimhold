#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Strips development-only loot test spawner objects from scenes during release (non-development) builds.
/// </summary>
public sealed class RaidLootTestSpawnerBuildProcessor : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report != null && (report.summary.options & BuildOptions.Development) == 0)
        {
            StripTestSpawners(scene);
        }
    }

    public static int StripTestSpawners(Scene scene)
    {
        int strippedCount = 0;
        var rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            var go = rootObjects[i];
            if (go == null) continue;

            if (go.name == "[LootTestSpawner]" || go.GetComponent<RaidLootTestSpawner>() != null)
            {
                Object.DestroyImmediate(go);
                strippedCount++;
            }
        }
        return strippedCount;
    }
}
#endif
