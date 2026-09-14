using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

namespace Spawning
{
    [DisallowMultipleComponent]
    public sealed class NetworkSpawnSceneConfiguration : MonoBehaviour
    {
        [FormerlySerializedAs("_spawnMode")]
        [SerializeField]
        private SceneSpawnPointPolicy _spawnPointPolicy = SceneSpawnPointPolicy.Required;

        [SerializeField]
        private PlayerSpawnAreaDefinition[] _playerSpawnAreas;

        [SerializeField]
        private SpawnGroupDefinition[] _spawnGroups;

        public SceneSpawnPointPolicy SpawnPointPolicy => _spawnPointPolicy;
        public IReadOnlyList<PlayerSpawnAreaDefinition> PlayerSpawnAreas => _playerSpawnAreas;
        public SpawnGroupDefinition[] SpawnGroups => _spawnGroups;

        public bool Validate(out string error)
        {
            error = string.Empty;

            // No spawn points allowed when policy is NotRequired
            if (_spawnPointPolicy == SceneSpawnPointPolicy.NotRequired)
            {
                if ((_playerSpawnAreas != null && _playerSpawnAreas.Length > 0) ||
                    (_spawnGroups != null && _spawnGroups.Length > 0))
                {
                    error = "Spatial player areas and spawn groups are not allowed when SceneSpawnPointPolicy is NotRequired.";
                    return false;
                }
                return true;
            }

            if (_playerSpawnAreas == null || _playerSpawnAreas.Length == 0)
            {
                error = "Player spawn areas must be configured when SceneSpawnPointPolicy is Required.";
                return false;
            }

            // Required requires valid non-player groups.
            if (_spawnGroups == null || _spawnGroups.Length == 0)
            {
                error = "Spatial spawn groups must be configured when SceneSpawnPointPolicy is Required.";
                return false;
            }

            var seenGroups = new System.Collections.Generic.HashSet<SpawnGroupType>();
            foreach (var definition in _spawnGroups)
            {
                if (definition == null)
                {
                    error = "SpawnGroupDefinition element is null.";
                    return false;
                }
                if (definition.Group == SpawnGroupType.Players)
                {
                    error = "Players must be configured through player spawn areas, not SpawnGroupDefinition.";
                    return false;
                }
                if (seenGroups.Contains(definition.Group))
                {
                    error = $"Duplicate spawn group type: {definition.Group}";
                    return false;
                }
                seenGroups.Add(definition.Group);

                if (!definition.IsValid())
                {
                    error = $"Invalid definition for group: {definition.Group}. Amount must be non-negative, and spawn points must be valid and not null.";
                    return false;
                }
            }
            return true;
        }
    }
}
