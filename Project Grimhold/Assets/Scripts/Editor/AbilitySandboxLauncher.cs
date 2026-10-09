using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-only launcher for the ability sandbox. The raid flow loads its gameplay scene by name from the
/// build settings, so this tool (1) temporarily enables <c>AbilitySandbox</c> in the build settings,
/// (2) opens MainMenu, (3) enters Play Mode, (4) points the runtime coordinator at the sandbox scene and
/// (5) starts the existing direct Host raid. The build-settings entry is removed when Play Mode ends, so
/// the sandbox scene never ships. No production script is modified.
/// </summary>
[InitializeOnLoad]
public static class AbilitySandboxLauncher
{
    private const string SandboxScenePath = "Assets/Scenes/AbilitySandbox.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string SandboxSceneName = "AbilitySandbox";
    private const string AddedKey = "AbilitySandbox.AddedToBuildSettings";
    private const string AutoStartKey = "AbilitySandbox.AutoStart";

    static AbilitySandboxLauncher()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(AddedKey, false))
        {
            RemoveFromBuildSettings();
        }
    }

    [MenuItem("Grimhold/Ability Sandbox/Play (Direct Host Raid)")]
    private static void Play()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[AbilitySandbox] Stop Play Mode before launching the sandbox.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SandboxScenePath) == null)
        {
            Debug.LogError($"[AbilitySandbox] Scene not found at {SandboxScenePath}.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EnsureInBuildSettings();
        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        SessionState.SetBool(AutoStartKey, true);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>Recovery after an editor crash left the sandbox scene in the build settings.</summary>
    [MenuItem("Grimhold/Ability Sandbox/Remove From Build Settings")]
    private static void RemoveFromBuildSettingsMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        RemoveFromBuildSettings();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(AutoStartKey, false))
        {
            SessionState.SetBool(AutoStartKey, false);
            EditorApplication.delayCall += StartDirectRaid;
        }
        else if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(AddedKey, false))
        {
            RemoveFromBuildSettings();
        }
    }

    private static void StartDirectRaid()
    {
        var starter = Object.FindFirstObjectByType<DirectRaidDevelopmentStarter>();
        var coordinator = Object.FindFirstObjectByType<SessionConnectionCoordinator>();
        if (starter == null || coordinator == null)
        {
            Debug.LogError(
                "[AbilitySandbox] MainMenu has no DirectRaidDevelopmentStarter / SessionConnectionCoordinator.");
            return;
        }

        var serialized = new SerializedObject(coordinator);
        SerializedProperty sceneName = serialized.FindProperty("_gameplaySceneName");
        if (sceneName == null)
        {
            Debug.LogError("[AbilitySandbox] SessionConnectionCoordinator._gameplaySceneName was not found.");
            return;
        }

        sceneName.stringValue = SandboxSceneName;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // The offline development profile is opt-in on the prefab. Enable it on the runtime instance only
        // so the direct raid does not need a real login; nothing is written back to the prefab or scene.
        var profileBootstrap = starter.GetComponent<DevelopmentProfileBootstrap>();
        var profileSerialized = new SerializedObject(profileBootstrap);
        SerializedProperty enabled = profileSerialized.FindProperty("_enabled");
        if (enabled == null)
        {
            Debug.LogError("[AbilitySandbox] DevelopmentProfileBootstrap._enabled was not found.");
            return;
        }

        enabled.boolValue = true;
        profileSerialized.ApplyModifiedPropertiesWithoutUndo();

        MethodInfo start = typeof(DirectRaidDevelopmentStarter).GetMethod(
            "StartDirectHostRaid", BindingFlags.Instance | BindingFlags.NonPublic);
        if (start == null)
        {
            Debug.LogError("[AbilitySandbox] DirectRaidDevelopmentStarter.StartDirectHostRaid was not found.");
            return;
        }

        Debug.Log("[AbilitySandbox] Starting the direct Host raid in the sandbox scene.");
        start.Invoke(starter, null);
    }

    private static void EnsureInBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == SandboxScenePath && scene.enabled))
        {
            return;
        }

        var updated = scenes.Where(scene => scene.path != SandboxScenePath).ToList();
        updated.Add(new EditorBuildSettingsScene(SandboxScenePath, true));
        EditorBuildSettings.scenes = updated.ToArray();
        SessionState.SetBool(AddedKey, true);
    }

    private static void RemoveFromBuildSettings()
    {
        EditorBuildSettings.scenes = EditorBuildSettings.scenes
            .Where(scene => scene.path != SandboxScenePath)
            .ToArray();
        SessionState.SetBool(AddedKey, false);
    }
}
