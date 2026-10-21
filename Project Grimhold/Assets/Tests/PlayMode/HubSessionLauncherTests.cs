#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fusion;
using System.Reflection;
using UnityEngine.SceneManagement;
using Assert = NUnit.Framework.Assert;

public class HubSessionLauncherTests
{
    private readonly HashSet<Scene> _initialScenes = new();
    private GameObject _launcherObject;
    private HubSessionLauncher _launcher;
    private Task<bool> _startupTask;
    private Scene _ownedTownScene;
    private string _initialSceneName;

    [SetUp]
    public void SetUp()
    {
        _initialScenes.Clear();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            _initialScenes.Add(SceneManager.GetSceneAt(i));
        _initialSceneName = SceneManager.GetActiveScene().name;
        _ownedTownScene = default;
        _startupTask = null;
        SceneManager.sceneLoaded += CaptureOwnedTownScene;
        LocalProfileProvider.SetRemoteCharacterId(new ProfileId("hub-session-test-character"));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // An unexpected log can abort the body while its asynchronous startup is still running.
        while (_startupTask != null && !_startupTask.IsCompleted)
            yield return null;
        SceneManager.sceneLoaded -= CaptureOwnedTownScene;

        Task<bool> shutdown = _launcher != null ? _launcher.ShutdownAndDestroyRunnerAsync() : null;
        while (shutdown != null && !shutdown.IsCompleted)
            yield return null;
        if (_launcherObject != null)
            Object.DestroyImmediate(_launcherObject);

        if (_ownedTownScene.IsValid() && _ownedTownScene.isLoaded)
        {
            // The launcher's Single load replaces the empty Test Runner scene. Unity cannot
            // unload its last scene: restore an empty test scene, not the saved bootstrap
            // asset (which would instantiate a second PlaymodeTestsController).
            if (SceneManager.sceneCount == 1)
                SceneManager.SetActiveScene(SceneManager.CreateScene(_initialSceneName));
            AsyncOperation unload = SceneManager.UnloadSceneAsync(_ownedTownScene);
            while (unload != null && !unload.isDone)
                yield return null;
        }

        LocalProfileProvider.ClearRemoteCharacterId();
        Assert.That(_ownedTownScene.isLoaded, Is.False, "The fixture must release its own Town scene.");
        Assert.That(shutdown == null || (!shutdown.IsFaulted && !shutdown.IsCanceled && shutdown.Result),
            Is.True, "The fixture must complete its owned runner shutdown.");
        _ownedTownScene = default;
        _launcher = null;
        _launcherObject = null;
    }

    private void CaptureOwnedTownScene(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == HubSessionLauncher.LobbyTownSessionName && !_initialScenes.Contains(scene))
            _ownedTownScene = scene;
    }

    [UnityTest]
    public IEnumerator Test_StartAndShutdownHubSession_DoesNotThrow()
    {
        _launcherObject = new GameObject("LauncherTest");
        Object.DontDestroyOnLoad(_launcherObject);
        var launcher = _launcher = _launcherObject.AddComponent<HubSessionLauncher>();
        FieldInfo socialPrefabField = typeof(HubSessionLauncher).GetField(
            "_socialPlayerPrefab",
            BindingFlags.Instance | BindingFlags.NonPublic);
        socialPrefabField.SetValue(
            launcher,
            new NetworkPrefabRef("b58bec13d63beb74ca61349f7d983c36"));

        bool startSuccess = false;

        LogAssert.Expect(
            UnityEngine.LogType.Error,
            "[TownProgressionPresenter] The local persistent profile context is unavailable.");
        var task = _startupTask = launcher.StartHubSessionAsync();

        // Convert async to coroutine
        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            Debug.LogException(task.Exception);
            NUnit.Framework.Assert.Fail("StartHubSessionAsync threw an exception.");
        }

        startSuccess = task.Result;
        NUnit.Framework.Assert.IsTrue(startSuccess, "Session should start successfully.");
        NUnit.Framework.Assert.IsNotNull(launcher.Runner, "Runner should be assigned.");
        NUnit.Framework.Assert.IsTrue(launcher.Runner.IsRunning, "Runner should be running.");
        NUnit.Framework.Assert.AreEqual(GameMode.Shared, launcher.Runner.GameMode, "GameMode should be Shared.");
        
        var shutdownTask = launcher.ShutdownAndDestroyRunnerAsync();
        while (!shutdownTask.IsCompleted)
        {
            yield return null;
        }
        
        if (shutdownTask.IsFaulted)
        {
            Debug.LogException(shutdownTask.Exception);
            NUnit.Framework.Assert.Fail("ShutdownAndDestroyRunnerAsync threw an exception.");
        }

        NUnit.Framework.Assert.IsNull(launcher.Runner, "Runner reference should be cleared after shutdown.");
    }
}
#endif
