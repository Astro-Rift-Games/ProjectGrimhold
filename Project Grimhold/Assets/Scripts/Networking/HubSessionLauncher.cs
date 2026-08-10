using Fusion;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class HubSessionLauncher : MonoBehaviour, ISessionRunnerOwner
{
    public const string LobbyTownSessionName = "Lobby-Town";

    private NetworkRunner _runner;
    private GameObject _runnerObject;
    private LauncherShutdownListener _shutdownListener;
    private bool _isStarting;

    [Header("Spawning")]
    [SerializeField]
    private NetworkPrefabRef _socialPlayerPrefab;

    public NetworkRunner Runner => _runner;
    public event Action<NetworkRunner, ShutdownReason> RunnerShutdownObserved;

    public Task<bool> StartHubSessionAsync(PlayerClassId selectedClass)
    {
        return StartHubSessionAsync(selectedClass, "Lobby-Town");
    }

    public async Task<bool> StartHubSessionAsync(PlayerClassId selectedClass, string townSceneName)
    {
        var profileId = LocalProfileProvider.GetOrCreateLocalProfile();
        var joinData = new PlayerJoinData(selectedClass, profileId);
        
        if (!PlayerJoinDataCodec.TryEncode(joinData, out byte[] token))
        {
            throw new ArgumentException($"Invalid or unsupported selected class: {selectedClass}");
        }

        int sceneBuildIndex = NetworkSceneBuildIndexResolver.Resolve(townSceneName);
        if (sceneBuildIndex < 0)
        {
            throw new ArgumentException($"Town scene '{townSceneName}' is not enabled in build settings.", nameof(townSceneName));
        }

        if (_isStarting || _runner != null)
            return false;

        _isStarting = true;

        try
        {
            if (!HubRunnerFactory.TryCreate(in joinData, _socialPlayerPrefab, out var composition))
            {
                Debug.LogError("[HubSessionLauncher] Failed to create runner composition via factory.", this);
                return false;
            }

            _runnerObject = composition.RunnerObject;
            _runner = composition.Runner;

            _shutdownListener = _runnerObject.AddComponent<LauncherShutdownListener>();
            _shutdownListener.Initialize(_runner, HandleRunnerShutdown);

            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(sceneBuildIndex), LoadSceneMode.Single);

            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = LobbyTownSessionName,
                ConnectionToken = token,
                Scene = sceneInfo,
                SceneManager = composition.SceneManager
            };

            StartGameResult result = await _runner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogError($"[HubSessionLauncher] Fusion failed to start Shared Mode. Reason: {result.ShutdownReason}", this);
                await ShutdownAndDestroyRunnerAsync();
                return false;
            }

            if (!await _shutdownListener.WaitForInitialSceneAsync())
            {
                Debug.LogError("[HubSessionLauncher] Town scene did not finish loading on the active runner.", this);
                await ShutdownAndDestroyRunnerAsync();
                return false;
            }

            Debug.Log($"[HubSessionLauncher] Fusion session started in Shared Mode. Session: {_runner.SessionInfo.Name}.", this);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            await ShutdownAndDestroyRunnerAsync();
            throw;
        }
        finally
        {
            _isStarting = false;
        }
    }

    public async Task<bool> ShutdownAndDestroyRunnerAsync()
    {
        NetworkRunner runner = _runner;
        GameObject runnerObject = _runnerObject;
        if (runner == null && runnerObject == null)
        {
            return true;
        }

        _shutdownListener?.Detach();
        _shutdownListener = null;
        bool succeeded = await RunnerShutdownUtility.ShutdownAndDestroyAsync(runner, runnerObject);
        ClearReferencesOnShutdown(runner);
        return succeeded;
    }

    public void ClearReferencesOnShutdown(NetworkRunner shutdownRunner)
    {
        if (_runner != shutdownRunner)
        {
            return;
        }

        _runner = null;
        _runnerObject = null;
        _shutdownListener = null;
    }

    private void HandleRunnerShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (_runner != runner)
        {
            return;
        }

        ClearReferencesOnShutdown(runner);
        RunnerShutdownObserved?.Invoke(runner, shutdownReason);
    }
}
