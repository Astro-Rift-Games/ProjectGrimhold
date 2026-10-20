using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Batch-only version configuration and fail-closed release metadata checks.</summary>
public static class ReleaseValidation
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    // Run in the disposable release candidate, never the developer's active project.
    public static void SetVersion()
    {
        RunBatch(() =>
        {
            string version = ReadArgument("-releaseVersion");
            RequireStableVersion(version);
            PlayerSettings.bundleVersion = version;
            AssetDatabase.SaveAssets();
            Debug.Log($"[ReleaseValidation] Set bundleVersion to {version}.");
        });
    }

    public static void Validate()
    {
        RunBatch(() =>
        {
            string version = ReadArgument("-releaseVersion");
            string baseline = ReadArgument("-baselineVersion");
            if (version != null && baseline != null)
                throw new InvalidOperationException("Supply only one of -releaseVersion or -baselineVersion.");
            if (version != null)
                RequireStableVersion(version);
            else
                version = baseline;

            if (string.IsNullOrWhiteSpace(version))
                throw new InvalidOperationException("Supply -releaseVersion, or -baselineVersion for a merge without a release.");
            if (!string.Equals(PlayerSettings.bundleVersion, version, StringComparison.Ordinal))
                throw new InvalidOperationException($"bundleVersion '{PlayerSettings.bundleVersion}' does not match expected '{version}'.");

            ValidateMainMenu();
            Debug.Log($"[ReleaseValidation] MainMenu version wiring and bundleVersion {version} are valid. Compilation and test results require separate checks.");
        });
    }

    private static void ValidateMainMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) == null)
            throw new InvalidOperationException($"Missing scene: {MainMenuScenePath}.");

        bool enabledInBuild = false;
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            enabledInBuild |= buildScene.enabled && buildScene.path == MainMenuScenePath;
        if (!enabledInBuild)
            throw new InvalidOperationException("MainMenu must be enabled in Build Settings.");

        Scene preview = EditorSceneManager.OpenPreviewScene(MainMenuScenePath);
        try
        {
            ApplicationVersionLabel label = null;
            int count = 0;
            foreach (GameObject root in preview.GetRootGameObjects())
            {
                foreach (ApplicationVersionLabel candidate in root.GetComponentsInChildren<ApplicationVersionLabel>(true))
                {
                    label = candidate;
                    count++;
                }
            }

            if (count != 1)
                throw new InvalidOperationException($"MainMenu requires exactly one ApplicationVersionLabel; found {count}.");
            if (!label.enabled || !label.gameObject.activeInHierarchy)
                throw new InvalidOperationException("MainMenu version label must be enabled and active.");

            var serializedLabel = new SerializedObject(label);
            var text = serializedLabel.FindProperty("_versionText").objectReferenceValue as TextMeshProUGUI;
            if (text == null || !text.enabled || text.gameObject != label.gameObject)
                throw new InvalidOperationException("Assign an enabled TextMeshProUGUI on the version label's own GameObject.");
            if (text.raycastTarget)
                throw new InvalidOperationException("Disable Raycast Target on the MainMenu version text.");
            Canvas canvas = label.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.enabled || label.transform.parent != canvas.transform || !canvas.isRootCanvas)
                throw new InvalidOperationException("Place the version label directly under the persistent root Canvas, outside toggled menu/login panels.");
            if (text.font == null || text.color.a <= 0f)
                throw new InvalidOperationException("Assign a visible TMP font and nontransparent color to the version label.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static void RequireStableVersion(string version)
    {
        if (version == null || !Regex.IsMatch(version, @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z"))
            throw new InvalidOperationException("-releaseVersion must be stable X.Y.Z without a v prefix.");
    }

    private static string ReadArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        string value = null;
        for (int i = 0; i < arguments.Length; i++)
        {
            if (!string.Equals(arguments[i], name, StringComparison.Ordinal))
                continue;
            if (value != null || i + 1 >= arguments.Length || arguments[i + 1].StartsWith("-", StringComparison.Ordinal))
                throw new InvalidOperationException($"Supply {name} exactly once with its value.");
            value = arguments[++i];
        }
        return value;
    }

    private static void RunBatch(Action action)
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("ReleaseValidation entry points require -batchmode.");
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[ReleaseValidation] {exception.Message}");
            EditorApplication.Exit(1);
            return;
        }
        EditorApplication.Exit(0);
    }
}
