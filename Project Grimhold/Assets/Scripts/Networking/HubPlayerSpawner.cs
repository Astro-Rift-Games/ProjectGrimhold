using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class HubPlayerSpawner : NetworkRunnerCallbacksAdapter
{
    private NetworkPrefabRef _socialPlayerPrefab;
    private HubSpawnSceneConfiguration _sceneConfiguration;
    private PlayerRef _pendingLocalPlayer;
    private bool _hasPendingLocalPlayer;

    public void Initialize(NetworkPrefabRef socialPlayerPrefab)
    {
        _socialPlayerPrefab = socialPlayerPrefab;
    }

    public override void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.LocalPlayer != player)
        {
            return;
        }

        _pendingLocalPlayer = player;
        _hasPendingLocalPlayer = true;
        TrySpawnPendingPlayer(runner);
    }

    public override void OnSceneLoadStart(NetworkRunner runner)
    {
        _sceneConfiguration = null;
    }

    public override void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!TryResolveSceneConfiguration(runner))
        {
            return;
        }

        TrySpawnPendingPlayer(runner);
    }

    private bool TryResolveSceneConfiguration(NetworkRunner runner)
    {
        if (runner.SceneManager == null)
        {
            Debug.LogError($"{nameof(HubPlayerSpawner)} requires a Fusion scene manager.", this);
            return false;
        }

        Scene runnerScene = runner.SceneManager.MainRunnerScene;
        if (!runnerScene.IsValid() || !runnerScene.isLoaded)
        {
            Debug.LogError($"{nameof(HubPlayerSpawner)} could not resolve the loaded Town scene.", this);
            return false;
        }

        HubSpawnSceneConfiguration found = null;
        GameObject[] roots = runnerScene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            HubSpawnSceneConfiguration[] configurations =
                roots[rootIndex].GetComponentsInChildren<HubSpawnSceneConfiguration>(true);
            for (int index = 0; index < configurations.Length; index++)
            {
                if (found != null && found != configurations[index])
                {
                    Debug.LogError($"Town scene '{runnerScene.name}' contains multiple {nameof(HubSpawnSceneConfiguration)} components.", configurations[index]);
                    return false;
                }

                found = configurations[index];
            }
        }

        if (found == null || found.SpawnPointCount == 0)
        {
            Debug.LogError($"Town scene '{runnerScene.name}' has no valid social spawn configuration.", this);
            return false;
        }

        _sceneConfiguration = found;
        return true;
    }

    private void TrySpawnPendingPlayer(NetworkRunner runner)
    {
        if (!_hasPendingLocalPlayer || _sceneConfiguration == null)
        {
            return;
        }

        PlayerRef player = _pendingLocalPlayer;
        if (runner.GetPlayerObject(player) != null)
        {
            _hasPendingLocalPlayer = false;
            return;
        }

        if (!_socialPlayerPrefab.IsValid ||
            !_sceneConfiguration.TryGetSpawnPose(player.RawEncoded, out Vector3 position, out Quaternion rotation))
        {
            Debug.LogError($"{nameof(HubPlayerSpawner)} cannot spawn the local social player because its prefab or spawn pose is invalid.", this);
            return;
        }

        NetworkObject playerObject = runner.Spawn(_socialPlayerPrefab, position, rotation, player);
        if (playerObject == null)
        {
            Debug.LogError($"{nameof(HubPlayerSpawner)} failed to spawn the local social player.", this);
            return;
        }

        runner.SetPlayerObject(player, playerObject);

        if (playerObject.TryGetBehaviour(out PlayerLootReceiver receiver))
        {
            var context = Object.FindAnyObjectByType<ApplicationStashContext>();
            if (context != null && context.Store != null)
            {
                var loadout = context.Store.GetLoadout();
                var entries = new System.Collections.Generic.List<LootEntry>(loadout.Count);
                foreach (var item in loadout)
                {
                    entries.Add(new LootEntry(item.LootId, item.Amount));
                }
                receiver.TryInitializeLoadout(entries, out _);
            }
        }

        _hasPendingLocalPlayer = false;
    }
}
