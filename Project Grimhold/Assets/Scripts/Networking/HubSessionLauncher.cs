using Fusion;
using System;
using System.Threading.Tasks;
using UnityEngine;

public sealed class HubSessionLauncher : MonoBehaviour
{
    public const string LobbyTownSessionName = "Lobby-Town";

    private NetworkRunner _runner;
    private GameObject _runnerObject;
    private bool _isStarting;

    [Header("Spawning")]
    [SerializeField]
    private NetworkPrefabRef _socialPlayerPrefab;

    [SerializeField]
    private Transform _spawnPoint;

    public NetworkRunner Runner => _runner;

    public async Task<bool> StartHubSessionAsync(PlayerClassId selectedClass)
    {
        var profileId = LocalProfileProvider.GetOrCreateLocalProfile();
        var joinData = new PlayerJoinData(selectedClass, profileId);
        
        if (!PlayerJoinDataCodec.TryEncode(joinData, out byte[] token))
        {
            throw new ArgumentException($"Invalid or unsupported selected class: {selectedClass}");
        }

        if (_isStarting || _runner != null)
            return false;

        _isStarting = true;

        try
        {
            if (!HubRunnerFactory.TryCreate(in joinData, _socialPlayerPrefab, _spawnPoint, out var composition))
            {
                Debug.LogError("[HubSessionLauncher] Failed to create runner composition via factory.", this);
                return false;
            }

            _runnerObject = composition.RunnerObject;
            _runner = composition.Runner;

            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = LobbyTownSessionName,
                ConnectionToken = token
            };

            StartGameResult result = await _runner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogError($"[HubSessionLauncher] Fusion failed to start Shared Mode. Reason: {result.ShutdownReason}", this);
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

    public async Task ShutdownAndDestroyRunnerAsync()
    {
        if (_runner != null)
        {
            if (_runner.IsRunning)
            {
                await _runner.Shutdown();
            }
            if (_runnerObject != null)
            {
                Destroy(_runnerObject);
            }
        }

        ClearReferencesOnShutdown(_runner);
    }

    public void ClearReferencesOnShutdown(NetworkRunner shutdownRunner)
    {
        if (_runner != shutdownRunner)
        {
            return;
        }

        _runner = null;
        _runnerObject = null;
    }
}
