using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HubPlayerSpawner : NetworkRunnerCallbacksAdapter
{
    private NetworkPrefabRef _socialPlayerPrefab;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;

    public void Initialize(NetworkPrefabRef socialPlayerPrefab, Transform spawnPoint)
    {
        _socialPlayerPrefab = socialPlayerPrefab;
        if (spawnPoint != null)
        {
            _spawnPosition = spawnPoint.position;
            _spawnRotation = spawnPoint.rotation;
        }
        else
        {
            _spawnPosition = Vector3.zero;
            _spawnRotation = Quaternion.identity;
        }
    }

    public override void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // In Shared Mode, state authority belongs to the client that spawns the object.
        // We only want to spawn the local player's avatar.
        if (runner.LocalPlayer == player)
        {
            if (_socialPlayerPrefab.IsValid)
            {
                runner.Spawn(
                    _socialPlayerPrefab,
                    _spawnPosition,
                    _spawnRotation,
                    player);
            }
            else
            {
                Debug.LogError("[HubPlayerSpawner] Social Player Prefab is not valid.");
            }
        }
    }
}
