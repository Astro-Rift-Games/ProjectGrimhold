using UnityEngine;

/// <summary>
/// Scene-owned source of social-player spawn poses for Lobby-Town.
/// The persistent connection coordinator never retains these scene transforms.
/// </summary>
[DisallowMultipleComponent]
public sealed class HubSpawnSceneConfiguration : MonoBehaviour
{
    [SerializeField]
    private Transform[] _spawnPoints;

    public int SpawnPointCount => _spawnPoints?.Length ?? 0;

    public bool TryGetSpawnPose(int stableIndex, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;

        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            return false;
        }

        int index = Mathf.Abs(stableIndex) % _spawnPoints.Length;
        Transform spawnPoint = _spawnPoints[index];
        if (spawnPoint == null)
        {
            return false;
        }

        position = spawnPoint.position;
        rotation = spawnPoint.rotation;
        return true;
    }

#if UNITY_EDITOR
    public void EditorSetSpawnPoints(Transform[] spawnPoints)
    {
        _spawnPoints = spawnPoints;
    }
#endif
}
