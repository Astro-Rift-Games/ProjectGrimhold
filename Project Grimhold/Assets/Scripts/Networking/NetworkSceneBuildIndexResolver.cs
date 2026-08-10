using System.IO;
using UnityEngine.SceneManagement;

/// <summary>
/// Resolves an enabled network scene from either its configured name or full asset path.
/// </summary>
public static class NetworkSceneBuildIndexResolver
{
    public static int Resolve(string sceneNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(sceneNameOrPath))
        {
            return -1;
        }

        for (int index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(index);
            if (string.Equals(path, sceneNameOrPath, System.StringComparison.Ordinal) ||
                string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    sceneNameOrPath,
                    System.StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
