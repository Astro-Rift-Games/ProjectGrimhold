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
        PlayerClassCatalog playerClassCatalog,
        NetworkPrefabRef raidParticipantPrefab,
        NetworkPrefabRef[] enemyPrefabs,
        in PlayerJoinData joinData,
        byte[] connectionToken,
        RaidLaunchManifest raidManifest,
        PendingLoadoutReservation loadoutReservation,
        out RunnerComposition composition)
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
                playerClassCatalog,
                raidParticipantPrefab,
                copiedEnemyPrefabs,
                startupContext,
                raidManifest))
        {
            Debug.LogError("[NetworkRunnerFactory] Failed to initialize NetworkSpawnManager.");
            Object.Destroy(runnerObject);
            return false;
        }

        var joinContext = runnerObject.AddComponent<LocalPlayerJoinContext>();
        if (raidManifest.IsValid)
        {
            RaidAdmissionData admission;
            if (loadoutReservation != null)
            {
                var reservedLoadout = new System.Collections.Generic.List<LootEntry>(loadoutReservation.Items.Count);
                for (int index = 0; index < loadoutReservation.Items.Count; index++)
                {
                    StashItem item = loadoutReservation.Items[index];
                    reservedLoadout.Add(new LootEntry(item.LootId, item.Amount));
                }

                admission = raidManifest.RaidCode.IsValid
                    ? new RaidAdmissionData(
                        raidManifest.RaidCode,
                        joinData.ProfileId,
                        joinData.ClassId,
                        loadoutReservation.ReservationId,
                        reservedLoadout)
                    : new RaidAdmissionData(
                        raidManifest.RaidId,
                        raidManifest.AccessSecret,
                        joinData.ProfileId,
                        joinData.ClassId,
                        loadoutReservation.ReservationId,
                        reservedLoadout);
            }
            else if (!RaidAdmissionDataCodec.TryDecode(connectionToken, out admission))
            {
                Debug.LogError("[NetworkRunnerFactory] Coordinated runner is missing valid admission data.");
                Object.Destroy(runnerObject);
                return false;
            }

            joinContext.Initialize(in joinData, in admission);
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
            playerClassCatalog,
            raidParticipantPrefab,
            copiedEnemyPrefabs,
            in joinData,
            copiedConnectionToken,
            in raidManifest);

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
