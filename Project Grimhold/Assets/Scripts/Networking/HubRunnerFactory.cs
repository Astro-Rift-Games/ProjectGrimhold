using Fusion;
using UnityEngine;

public static class HubRunnerFactory
{
    public readonly struct HubRunnerComposition
    {
        public readonly GameObject RunnerObject;
        public readonly NetworkRunner Runner;
        public readonly NetworkSceneManagerDefault SceneManager;

        public HubRunnerComposition(
            GameObject runnerObject,
            NetworkRunner runner,
            NetworkSceneManagerDefault sceneManager)
        {
            RunnerObject = runnerObject;
            Runner = runner;
            SceneManager = sceneManager;
        }
    }

    public static bool TryCreate(
        in PlayerJoinData joinData,
        NetworkPrefabRef socialPlayerPrefab,
        out HubRunnerComposition composition)
    {
        composition = default;

        GameObject runnerObject = new GameObject("HubNetworkRunner");
        NetworkRunner runner = runnerObject.AddComponent<NetworkRunner>();
        var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
        runnerObject.AddComponent<EntityRegistry>();
        runnerObject.AddComponent<LocalInputContext>();
        
        var joinContext = runnerObject.AddComponent<LocalPlayerJoinContext>();
        joinContext.Initialize(in joinData);
        
        var spawner = runnerObject.AddComponent<HubPlayerSpawner>();
        spawner.Initialize(socialPlayerPrefab);
        
        Object.DontDestroyOnLoad(runnerObject);
        runner.ProvideInput = true;

        composition = new HubRunnerComposition(runnerObject, runner, sceneManager);
        return true;
    }
}
