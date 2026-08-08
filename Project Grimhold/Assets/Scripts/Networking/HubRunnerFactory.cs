using Fusion;
using UnityEngine;

public static class HubRunnerFactory
{
    public readonly struct HubRunnerComposition
    {
        public readonly GameObject RunnerObject;
        public readonly NetworkRunner Runner;

        public HubRunnerComposition(GameObject runnerObject, NetworkRunner runner)
        {
            RunnerObject = runnerObject;
            Runner = runner;
        }
    }

    public static bool TryCreate(in PlayerJoinData joinData, NetworkPrefabRef socialPlayerPrefab, Transform spawnPoint, out HubRunnerComposition composition)
    {
        composition = default;

        GameObject runnerObject = new GameObject("HubNetworkRunner");
        NetworkRunner runner = runnerObject.AddComponent<NetworkRunner>();
        
        runnerObject.AddComponent<LocalInputContext>();
        
        var joinContext = runnerObject.AddComponent<LocalPlayerJoinContext>();
        joinContext.Initialize(in joinData);
        
        var spawner = runnerObject.AddComponent<HubPlayerSpawner>();
        spawner.Initialize(socialPlayerPrefab, spawnPoint);
        
        Object.DontDestroyOnLoad(runnerObject);
        runner.ProvideInput = true;

        composition = new HubRunnerComposition(runnerObject, runner);
        return true;
    }
}
