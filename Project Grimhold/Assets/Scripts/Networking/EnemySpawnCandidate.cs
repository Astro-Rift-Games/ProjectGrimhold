using System;

namespace Spawning
{
    /// <summary>
    /// Represents an enemy archetype candidate available for weighted spawner selection.
    /// Encapsulates its payload (e.g. NetworkPrefabRef, GameObject or identifier),
    /// spawn weight, threat cost, and environmental restriction.
    /// </summary>
    [Serializable]
    public struct EnemySpawnCandidate<T>
    {
        public T Item;
        public T Prefab => Item;
        public string EnemyId;
        public int Weight;
        public int ThreatCost;
        public string RequiredEnvironment;

        public EnemySpawnCandidate(T item, string enemyId, int weight, int threatCost, string requiredEnvironment = null)
        {
            Item = item;
            EnemyId = enemyId ?? string.Empty;
            Weight = Math.Max(0, weight);
            ThreatCost = Math.Max(1, threatCost);
            RequiredEnvironment = requiredEnvironment;
        }

        public bool IsEnvironmentEligible(string currentEnvironment)
        {
            if (string.IsNullOrEmpty(RequiredEnvironment))
            {
                // No environment restriction: eligible in any dungeon
                return true;
            }

            if (string.IsNullOrEmpty(currentEnvironment))
            {
                return false;
            }

            return string.Equals(RequiredEnvironment, currentEnvironment, StringComparison.OrdinalIgnoreCase);
        }
    }
}
