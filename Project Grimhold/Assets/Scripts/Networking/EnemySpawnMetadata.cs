using UnityEngine;

namespace Spawning
{
    /// <summary>
    /// Explicit authoring component placed on enemy prefabs to declare
    /// spawn weight, threat cost, and environmental requirements in the Inspector.
    /// If absent, EnemySpawnCandidateResolver automatically derives defaults from the enemy archetype.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawnMetadata : MonoBehaviour
    {
        [SerializeField, Tooltip("Archetype identifier (e.g. 'slime', 'skeleton', 'specter').")]
        private string _enemyId = "enemy";

        [SerializeField, Min(1), Tooltip("Relative spawn probability weight within eligible pools.")]
        private int _spawnWeight = 45;

        [SerializeField, Min(1), Tooltip("Threat cost consumed against room or wave threat budget.")]
        private int _threatCost = 1;

        [SerializeField, Tooltip("Required dungeon environment (e.g. 'catacombs'). Empty means eligible anywhere.")]
        private string _requiredEnvironment = string.Empty;

        public string EnemyId => _enemyId;
        public int SpawnWeight => _spawnWeight;
        public int ThreatCost => _threatCost;
        public string RequiredEnvironment => _requiredEnvironment;

        public void Configure(string enemyId, int spawnWeight, int threatCost, string requiredEnvironment = "")
        {
            _enemyId = enemyId ?? string.Empty;
            _spawnWeight = Mathf.Max(1, spawnWeight);
            _threatCost = Mathf.Max(1, threatCost);
            _requiredEnvironment = requiredEnvironment ?? string.Empty;
        }
    }
}
