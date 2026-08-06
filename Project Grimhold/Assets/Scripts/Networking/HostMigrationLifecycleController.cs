using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class HostMigrationLifecycleController : NetworkRunnerCallbacksAdapter
{
    private NetworkRunner _associatedRunner;
    private PlayerClassCatalog _playerClassCatalog;
    private NetworkPrefabRef[] _enemyPrefabs;
    private PlayerJoinData _joinData;
    private byte[] _connectionToken;
    private bool _isMigrating;

    public void Initialize(
        NetworkRunner runner,
        PlayerClassCatalog playerClassCatalog,
        NetworkPrefabRef[] enemyPrefabs,
        in PlayerJoinData joinData,
        byte[] connectionToken)
    {
        _associatedRunner = runner;
        _playerClassCatalog = playerClassCatalog;
        _enemyPrefabs = enemyPrefabs;
        _joinData = joinData; // Struct copy
        _connectionToken = connectionToken;
    }

    public override void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        if (runner != _associatedRunner)
            return;

        if (_isMigrating)
        {
            Debug.LogWarning("[HostMigrationLifecycleController] A migration is already in progress, rejecting duplicate OnHostMigration call.");
            return;
        }

        _isMigrating = true;
        
        // Ejecutar migración asincrónica
        _ = HandleHostMigrationAsync(runner, hostMigrationToken);
    }

    private async Task HandleHostMigrationAsync(NetworkRunner oldRunner, HostMigrationToken token)
    {
        try
        {
            var spawnManager = oldRunner.GetComponent<NetworkSpawnManager>();
            var matchController = spawnManager != null ? spawnManager.MatchController : null;
            bool isActiveExpedition = matchController != null && matchController.Phase == NetworkMatchController.MatchPhase.InProgress;

            if (!isActiveExpedition)
            {
                Debug.LogWarning("[HostMigrationLifecycleController] Host migration triggered outside of an active expedition. Rejecting migration.");
                await oldRunner.Shutdown(destroyGameObject: false, shutdownReason: ShutdownReason.HostMigration);
                
                if (oldRunner != null && oldRunner.gameObject != null)
                {
                    Destroy(oldRunner.gameObject);
                }
                return;
            }

            Debug.Log("[HostMigrationLifecycleController] Starting host migration...");

            GameObject oldRunnerObject = oldRunner.gameObject;
            var oldScene = oldRunner.SceneManager.MainRunnerScene;
            int oldSceneBuildIndex = oldScene.buildIndex;
            GameMode mode = token.GameMode;

            if (!oldScene.IsValid() || !oldScene.isLoaded || oldSceneBuildIndex < 0)
            {
                throw new InvalidOperationException("The main runner scene is invalid or not loaded. Cannot migrate host.");
            }

            Debug.Log("[HostMigrationLifecycleController] Shutting down old runner with HostMigration reason.");
            await oldRunner.Shutdown(destroyGameObject: false, shutdownReason: ShutdownReason.HostMigration);
            
            string tempSceneName = $"HostMigrationTemp_{Guid.NewGuid():N}";
            Scene temporaryScene = SceneManager.CreateScene(tempSceneName);
            if (!temporaryScene.IsValid() || !temporaryScene.isLoaded)
            {
                throw new InvalidOperationException("Failed to create temporary migration scene.");
            }
            if (!SceneManager.SetActiveScene(temporaryScene))
            {
                throw new InvalidOperationException("Failed to set temporary migration scene as active.");
            }

            Debug.Log($"[HostMigrationLifecycleController] Unloading old scene (index {oldSceneBuildIndex})...");
            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(oldScene);
            if (unloadOperation != null)
            {
                await unloadOperation;
            }
            
            if (oldScene.isLoaded)
            {
                throw new InvalidOperationException("Old scene failed to unload fully.");
            }

            Debug.Log("[HostMigrationLifecycleController] Creating replacement runner...");
            if (!NetworkRunnerFactory.TryCreate(
                mode,
                SessionStartupContext.HostMigrationResume,
                _playerClassCatalog,
                _enemyPrefabs,
                in _joinData,
                _connectionToken,
                out var newComposition))
            {
                throw new InvalidOperationException("Failed to create replacement runner via factory.");
            }

            SceneRef sceneRef = SceneRef.FromIndex(oldSceneBuildIndex);
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);

            var startGameArgs = new StartGameArgs
            {
                GameMode = mode,
                HostMigrationToken = token,
                HostMigrationResume = newComposition.HostMigrationController.HostMigrationResumeCallback,
                ConnectionToken = _connectionToken,
                Scene = sceneInfo
            };

            Debug.Log("[HostMigrationLifecycleController] Starting new runner with HostMigrationToken...");
            StartGameResult result = await newComposition.Runner.StartGame(startGameArgs);

            if (!result.Ok)
            {
                Debug.LogError($"[HostMigrationLifecycleController] StartGame failed during migration. Reason: {result.ShutdownReason}");
                if (newComposition.Runner != null && newComposition.Runner.IsRunning)
                {
                    await newComposition.Runner.Shutdown();
                }
                if (newComposition.RunnerObject != null)
                {
                    Destroy(newComposition.RunnerObject);
                }
            }
            else
            {
                Debug.Log("[HostMigrationLifecycleController] Host migration completed successfully.");
            }

            // Destruir el GameObject del runner viejo cuando sea seguro (al finalizar StartGame con o sin exito)
            if (oldRunnerObject != null)
            {
                Destroy(oldRunnerObject);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            _isMigrating = false; 
            // Podríamos intentar destruir el object viejo, pero depende del estado en el que falló.
            // Si falló antes de destruir, evitamos leaks.
            if (oldRunner != null && oldRunner.gameObject != null)
            {
                Destroy(oldRunner.gameObject);
            }
        }
    }

    public void HostMigrationResumeCallback(NetworkRunner runner)
    {
        if (runner != _associatedRunner)
            return;

        Debug.Log("[HostMigrationLifecycleController] HostMigrationResume callback invoked. Waiting for resumed scene-load pipeline.");
        // La barrera real (AwaitingHostMigrationRestore) se verifica en OnSceneLoadDone del NetworkSpawnManager.
    }
}
