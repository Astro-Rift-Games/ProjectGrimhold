using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces a disconnected raid Host on the surviving peer and hands the restored
/// runner composition back to the existing <see cref="FusionSessionLauncher"/> owner.
/// </summary>
[DisallowMultipleComponent]
public sealed class HostMigrationLifecycleController : NetworkRunnerCallbacksAdapter
{
    private static readonly TimeSpan MigrationCompletionTimeout = TimeSpan.FromSeconds(30);

    private NetworkRunner _associatedRunner;
    private PlayerClassCatalog _playerClassCatalog;
    private NetworkPrefabRef _raidParticipantPrefab;
    private NetworkPrefabRef[] _enemyPrefabs;
    private PlayerJoinData _joinData;
    private byte[] _connectionToken;
    private RaidLaunchManifest _raidManifest;
    private FusionSessionLauncher _runnerOwner;
    private bool _isMigrating;

    public void Initialize(
        NetworkRunner runner,
        PlayerClassCatalog playerClassCatalog,
        NetworkPrefabRef raidParticipantPrefab,
        NetworkPrefabRef[] enemyPrefabs,
        in PlayerJoinData joinData,
        byte[] connectionToken,
        in RaidLaunchManifest raidManifest,
        FusionSessionLauncher runnerOwner)
    {
        _associatedRunner = runner;
        _playerClassCatalog = playerClassCatalog;
        _raidParticipantPrefab = raidParticipantPrefab;
        _enemyPrefabs = enemyPrefabs;
        _joinData = joinData;
        _connectionToken = connectionToken;
        _raidManifest = raidManifest;
        _runnerOwner = runnerOwner;
    }

    public override void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        if (runner != _associatedRunner)
        {
            return;
        }

        if (_isMigrating)
        {
            Debug.LogWarning(
                "[HOST-RETURN-MIGRATION] Duplicate OnHostMigration ignored while migration is active.",
                this);
            return;
        }

        if (_runnerOwner == null || !_runnerOwner.TryBeginHostMigration(runner))
        {
            Debug.LogError(
                "[HOST-RETURN-MIGRATION] OnHostMigration could not register migration ownership with the launcher.",
                this);
            return;
        }

        _isMigrating = true;
        Debug.Log(
            "[HOST-RETURN-MIGRATION] OnHostMigration received; migration now owns runner replacement.",
            this);
        _ = HandleHostMigrationAsync(runner, hostMigrationToken);
    }

    private async Task HandleHostMigrationAsync(NetworkRunner oldRunner, HostMigrationToken token)
    {
        NetworkRunnerFactory.RunnerComposition replacement = default;
        GameObject oldRunnerObject = oldRunner != null ? oldRunner.gameObject : null;
        try
        {
            NetworkSpawnManager oldSpawnManager = oldRunner.GetComponent<NetworkSpawnManager>();
            NetworkMatchController oldMatchController = oldSpawnManager != null
                ? oldSpawnManager.MatchController
                : null;
            if (oldMatchController == null ||
                oldMatchController.Phase != NetworkMatchController.MatchPhase.InProgress)
            {
                throw new InvalidOperationException(
                    "Host migration triggered outside of an active InProgress expedition.");
            }

            Scene oldScene = oldRunner.SceneManager.MainRunnerScene;
            int oldSceneBuildIndex = oldScene.buildIndex;
            if (!oldScene.IsValid() || !oldScene.isLoaded || oldSceneBuildIndex < 0)
            {
                throw new InvalidOperationException(
                    "The main runner scene is invalid or not loaded. Cannot migrate Host.");
            }

            GameMode mode = token.GameMode;
            Debug.Log(
                "[HOST-RETURN-MIGRATION] Shutting down old Client runner with ShutdownReason.HostMigration.",
                this);
            await oldRunner.Shutdown(
                destroyGameObject: false,
                shutdownReason: ShutdownReason.HostMigration);

            string temporarySceneName = $"HostMigrationTemp_{Guid.NewGuid():N}";
            Scene temporaryScene = SceneManager.CreateScene(temporarySceneName);
            if (!temporaryScene.IsValid() || !temporaryScene.isLoaded ||
                !SceneManager.SetActiveScene(temporaryScene))
            {
                throw new InvalidOperationException("Failed to prepare the temporary migration scene.");
            }

            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(oldScene);
            if (unloadOperation != null)
            {
                await unloadOperation;
            }

            if (oldScene.isLoaded)
            {
                throw new InvalidOperationException("Old raid scene failed to unload fully.");
            }

            Debug.Log("[HOST-RETURN-MIGRATION] Creating replacement runner composition.", this);
            if (!NetworkRunnerFactory.TryCreate(
                    mode,
                    SessionStartupContext.HostMigrationResume,
                    _playerClassCatalog,
                    _raidParticipantPrefab,
                    _enemyPrefabs,
                    in _joinData,
                    _connectionToken,
                    _raidManifest,
                    null,
                    _runnerOwner,
                    out replacement))
            {
                throw new InvalidOperationException("Failed to create replacement runner via factory.");
            }

            var sceneInfo = new NetworkSceneInfo();
            if (sceneInfo.AddSceneRef(
                    SceneRef.FromIndex(oldSceneBuildIndex),
                    LoadSceneMode.Single) < 0)
            {
                throw new InvalidOperationException("Failed to add raid scene to replacement runner.");
            }

            var startGameArgs = new StartGameArgs
            {
                GameMode = mode,
                HostMigrationToken = token,
                HostMigrationResume = replacement.SnapshotRestorer.HostMigrationResumeCallback,
                ConnectionToken = _connectionToken,
                Scene = sceneInfo,
                IsOpen = true,
                IsVisible = false
            };

            Debug.Log(
                "[HOST-RETURN-MIGRATION] Starting replacement runner with HostMigrationToken.",
                this);
            StartGameResult result = await replacement.Runner.StartGame(startGameArgs);
            if (!result.Ok)
            {
                Debug.LogError(
                    $"[HOST-RETURN-MIGRATION] Replacement StartGame failed. " +
                    $"Reason={result.ShutdownReason}.",
                    this);
                throw new InvalidOperationException(
                    $"Replacement StartGame failed with {result.ShutdownReason}.");
            }

            Debug.Log(
                "[HOST-RETURN-MIGRATION] Replacement StartGame returned OK; " +
                "waiting for migration completion.",
                this);
            NetworkSpawnManager.HostMigrationCompletionResult completion =
                await replacement.SpawnManager.WaitForHostMigrationCompletionAsync(
                    MigrationCompletionTimeout);
            if (!completion.Succeeded)
            {
                if (completion.Status ==
                    NetworkSpawnManager.HostMigrationCompletionStatus.Timeout)
                {
                    Debug.LogError(
                        $"[HOST-RETURN-MIGRATION] Migration completion timeout after " +
                        $"{MigrationCompletionTimeout.TotalSeconds:0} seconds. " +
                        completion.Details,
                        this);
                    throw new TimeoutException(
                        $"Migration completion timeout. {completion.Details}");
                }

                Debug.LogError(
                    $"[HOST-RETURN-MIGRATION] Migration completion FAILURE. " +
                    completion.Details,
                    this);
                throw new InvalidOperationException(
                    $"Migration completion failed. {completion.Details}");
            }

            Debug.Log(
                "[HOST-RETURN-MIGRATION] Migration completion SUCCESS; " +
                "validating final runner invariants.",
                this);
            if (replacement.Runner == null || !replacement.Runner.IsRunning ||
                !replacement.Runner.IsServer)
            {
                throw new InvalidOperationException(
                    "Replacement runner is not an active server after migration completion.");
            }

            NetworkMatchController restoredMatchController = replacement.SpawnManager.MatchController;
            if (restoredMatchController == null ||
                restoredMatchController.Phase != NetworkMatchController.MatchPhase.InProgress)
            {
                throw new InvalidOperationException(
                    $"Restored MatchPhase is not InProgress " +
                    $"({restoredMatchController?.Phase.ToString() ?? "missing"}).");
            }

            if (!_runnerOwner.TryAdoptMigratedRunner(oldRunner, in replacement))
            {
                throw new InvalidOperationException(
                    "FusionSessionLauncher rejected replacement runner adoption.");
            }

            Debug.Log(
                $"[HOST-RETURN-MIGRATION] Host Migration completed. " +
                $"Replacement adopted with MatchPhase={restoredMatchController.Phase}.",
                this);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            await CleanupFailedReplacementAsync(replacement.Runner, replacement.RunnerObject);

            if (oldRunnerObject != null)
            {
                Destroy(oldRunnerObject);
                while (oldRunnerObject != null)
                {
                    await Task.Yield();
                }
            }

            _runnerOwner?.ReportHostMigrationFailure(oldRunner, exception.Message);
        }
        finally
        {
            _isMigrating = false;
            if (oldRunnerObject != null)
            {
                Destroy(oldRunnerObject);
            }
        }
    }

    private async Task CleanupFailedReplacementAsync(
        NetworkRunner replacementRunner,
        GameObject replacementObject)
    {
        if (replacementRunner != null && replacementRunner.IsRunning)
        {
            try
            {
                await replacementRunner.Shutdown(
                    destroyGameObject: false,
                    shutdownReason: ShutdownReason.Error);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        if (replacementObject == null)
        {
            return;
        }

        Destroy(replacementObject);
        while (replacementObject != null)
        {
            await Task.Yield();
        }
    }
}
