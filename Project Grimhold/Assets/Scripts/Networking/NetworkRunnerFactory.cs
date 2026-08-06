using Fusion;
using UnityEngine;

public static class NetworkRunnerFactory
{
    public readonly struct RunnerComposition
    {
        public readonly GameObject RunnerObject;
        public readonly NetworkRunner Runner;
        public readonly NetworkSpawnManager SpawnManager;
        public readonly ExtractionSanctuaryAssignmentService SanctuaryAssignmentService;
        public readonly HostMigrationLifecycleController HostMigrationController;

        public RunnerComposition(
            GameObject runnerObject,
            NetworkRunner runner,
            NetworkSpawnManager spawnManager,
            ExtractionSanctuaryAssignmentService sanctuaryAssignmentService,
            HostMigrationLifecycleController hostMigrationController)
        {
            RunnerObject = runnerObject;
            Runner = runner;
            SpawnManager = spawnManager;
            SanctuaryAssignmentService = sanctuaryAssignmentService;
            HostMigrationController = hostMigrationController;
        }
    }

    public static bool TryCreate(
        GameMode mode,
        SessionStartupContext startupContext,
        PlayerClassCatalog playerClassCatalog,
        NetworkPrefabRef[] enemyPrefabs,
        in PlayerJoinData joinData,
        byte[] connectionToken,
        out RunnerComposition composition)
    {
        composition = default;

        GameObject runnerObject = new GameObject("NetworkRunner");
        NetworkRunner runner = runnerObject.AddComponent<NetworkRunner>();
        runnerObject.AddComponent<EntityRegistry>();
        
        var sanctuaryAssignmentService = runnerObject.AddComponent<ExtractionSanctuaryAssignmentService>();
        if (!sanctuaryAssignmentService.Initialize(runner, mode))
        {
            Debug.LogError("[NetworkRunnerFactory] Failed to initialize ExtractionSanctuaryAssignmentService.");
            Object.Destroy(runnerObject);
            return false;
        }

        runnerObject.AddComponent<LocalInputContext>();

        NetworkPrefabRef[] copiedEnemyPrefabs = enemyPrefabs != null
            ? (NetworkPrefabRef[])enemyPrefabs.Clone()
            : null;

        var spawnManager = runnerObject.AddComponent<NetworkSpawnManager>();
        if (!spawnManager.InitializeForRunner(runner, playerClassCatalog, copiedEnemyPrefabs, startupContext))
        {
            Debug.LogError("[NetworkRunnerFactory] Failed to initialize NetworkSpawnManager.");
            Object.Destroy(runnerObject);
            return false;
        }

        var joinContext = runnerObject.AddComponent<LocalPlayerJoinContext>();
        joinContext.Initialize(in joinData);

        byte[] copiedConnectionToken = connectionToken != null
            ? (byte[])connectionToken.Clone()
            : null;

        var hostMigrationController = runnerObject.AddComponent<HostMigrationLifecycleController>();
        hostMigrationController.Initialize(
            runner,
            playerClassCatalog,
            copiedEnemyPrefabs,
            in joinData,
            copiedConnectionToken);

        Object.DontDestroyOnLoad(runnerObject);
        runner.ProvideInput = true;

        composition = new RunnerComposition(
            runnerObject,
            runner,
            spawnManager,
            sanctuaryAssignmentService,
            hostMigrationController);

        return true;
    }
}
