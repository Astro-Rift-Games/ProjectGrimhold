using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>Never ship a stale/missing dungeon minimap after an authored design or art change.</summary>
public sealed class MinimapLayoutBuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!MinimapLayoutGenerator.EnsureCurrentLayout(out string error))
            throw new BuildFailedException(error);
    }
}
