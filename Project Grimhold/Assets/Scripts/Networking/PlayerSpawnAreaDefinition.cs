using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spawning
{
    /// <summary>
    /// Ordered spatial positions available to one initial Raid team.
    /// Team-to-area assignment is resolved from the frozen launch context.
    /// </summary>
    [Serializable]
    public sealed class PlayerSpawnAreaDefinition
    {
        [SerializeField]
        private Transform[] _spawnPoints;

        public IReadOnlyList<Transform> SpawnPoints => _spawnPoints;

        public PlayerSpawnAreaDefinition(Transform[] spawnPoints)
        {
            _spawnPoints = spawnPoints;
        }
    }
}
