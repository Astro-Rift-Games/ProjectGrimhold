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

        if (_spawnPoints == null || _spawnPoints.Length < RaidSessionRules.MaxParticipants ||
            stableIndex < 0 || stableIndex >= _spawnPoints.Length)
        {
            return false;
        }

        Transform spawnPoint = _spawnPoints[stableIndex];
        if (spawnPoint == null)
        {
            return false;
        }

        position = spawnPoint.position;
        rotation = spawnPoint.rotation;
        return true;
    }

    public bool Validate(out string failure)
    {
        failure = null;
        if (_spawnPoints == null || _spawnPoints.Length < RaidSessionRules.MaxParticipants)
        {
            failure = $"Town requires at least {RaidSessionRules.MaxParticipants} social spawn points.";
            return false;
        }

        for (int index = 0; index < _spawnPoints.Length; index++)
        {
            if (_spawnPoints[index] == null)
            {
                failure = $"Town social spawn point {index} is null.";
                return false;
            }

            for (int other = index + 1; other < _spawnPoints.Length; other++)
            {
                if (_spawnPoints[other] != null && _spawnPoints[index].position == _spawnPoints[other].position)
                {
                    failure = $"Town social spawn points {index} and {other} share the same position.";
                    return false;
                }
            }
        }

        return true;
    }

#if UNITY_EDITOR
    public void EditorSetSpawnPoints(Transform[] spawnPoints)
    {
        _spawnPoints = spawnPoints;
    }
#endif
}
