using Fusion;
using UnityEngine;

public static class NetworkRunnerFactory
{
    public readonly struct RunnerComposition
    {
        public readonly GameObject RunnerObject;
        public readonly NetworkRunner Runner;
        public readonly NetworkSceneManagerDefault SceneManager;
        public readonly NetworkSpawnManager SpawnManager;
        public readonly ExtractionSanctuaryAssignmentService SanctuaryAssignmentService;
        public readonly HostMigrationLifecycleController HostMigrationController;
        public readonly HostMigrationSnapshotRestorer SnapshotRestorer;

        public RunnerComposition(
            GameObject runnerObject,
            NetworkRunner runner,
            NetworkSceneManagerDefault sceneManager,
            NetworkSpawnManager spawnManager,
            ExtractionSanctuaryAssignmentService sanctuaryAssignmentService,
            HostMigrationLifecycleController hostMigrationController,
            HostMigrationSnapshotRestorer snapshotRestorer)
        {
            RunnerObject = runnerObject;
            Runner = runner;
            SceneManager = sceneManager;
            SpawnManager = spawnManager;
            SanctuaryAssignmentService = sanctuaryAssignmentService;
            HostMigrationController = hostMigrationController;
            SnapshotRestorer = snapshotRestorer;
        }
    }

    public static bool TryCreate(
        GameMode mode,
        SessionStartupContext startupContext,
        NetworkPrefabRef raidPlayerPrefab,
        NetworkPrefabRef raidParticipantPrefab,
        NetworkPrefabRef[] enemyPrefabs,
        in PlayerJoinData joinData,
        byte[] connectionToken,
        RaidLaunchContext launchContext,
        PendingLoadoutReservation loadoutReservation,
        FusionSessionLauncher runnerOwner,
        out RunnerComposition composition,
        in RaidAdmissionData admissionData = default)
    {
        composition = default;

        GameObject runnerObject = new GameObject("NetworkRunner");
        NetworkRunner runner = runnerObject.AddComponent<NetworkRunner>();
        var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
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
        if (!spawnManager.InitializeForRunner(
                runner,
                raidPlayerPrefab,
                raidParticipantPrefab,
                copiedEnemyPrefabs,
                startupContext,
                launchContext))
        {
            Debug.LogError("[NetworkRunnerFactory] Failed to initialize NetworkSpawnManager.");
            Object.Destroy(runnerObject);
            return false;
        }

        var joinContext = runnerObject.AddComponent<LocalPlayerJoinContext>();
        RaidAdmissionData resolvedAdmission = default;
        if (launchContext != null)
        {
            resolvedAdmission = admissionData;
            if (!resolvedAdmission.IsValid)
            {
                RaidAdmissionDataCodec.TryDecode(connectionToken, out resolvedAdmission);
            }

            if (!resolvedAdmission.IsValid && startupContext.Mode != SessionStartupMode.HostMigrationResume)
            {
                Debug.LogError("[NetworkRunnerFactory] Coordinated runner is missing valid admission data.");
                Object.Destroy(runnerObject);
                return false;
            }

            joinContext.Initialize(in joinData, in resolvedAdmission);
        }
        else
        {
            joinContext.Initialize(in joinData);
        }

        byte[] copiedConnectionToken = connectionToken != null
            ? (byte[])connectionToken.Clone()
            : null;

        var hostMigrationController = runnerObject.AddComponent<HostMigrationLifecycleController>();
        hostMigrationController.Initialize(
            runner,
            raidPlayerPrefab,
            raidParticipantPrefab,
            copiedEnemyPrefabs,
            in joinData,
            copiedConnectionToken,
            launchContext,
            runnerOwner,
            in resolvedAdmission);

        var snapshotRestorer = runnerObject.AddComponent<HostMigrationSnapshotRestorer>();
        snapshotRestorer.Initialize(runner, startupContext, spawnManager);

        Object.DontDestroyOnLoad(runnerObject);
        runner.ProvideInput = true;

        composition = new RunnerComposition(
            runnerObject,
            runner,
            sceneManager,
            spawnManager,
            sanctuaryAssignmentService,
            hostMigrationController,
            snapshotRestorer);

        return true;
    }
}
