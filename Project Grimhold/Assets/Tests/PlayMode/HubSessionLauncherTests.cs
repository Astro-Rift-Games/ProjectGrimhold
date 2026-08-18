#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fusion;
using System.Reflection;

public class HubSessionLauncherTests
{
    [UnityTest]
    public IEnumerator Test_StartAndShutdownHubSession_DoesNotThrow()
    {
        var go = new GameObject("LauncherTest");
        var launcher = go.AddComponent<HubSessionLauncher>();
        FieldInfo socialPrefabField = typeof(HubSessionLauncher).GetField(
            "_socialPlayerPrefab",
            BindingFlags.Instance | BindingFlags.NonPublic);
        socialPrefabField.SetValue(
            launcher,
            new NetworkPrefabRef("b58bec13d63beb74ca61349f7d983c36"));

        bool startSuccess = false;

        var task = launcher.StartHubSessionAsync();

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
