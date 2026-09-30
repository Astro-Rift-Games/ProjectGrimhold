using System;
using UnityEngine;

namespace Spawning
{
    [Serializable]
    public struct ReinforcementPolicy
    {
        [Tooltip("Number of enemies this phase can spawn. 0 means no spawns.")]
        public int Budget;
        
        [Tooltip("Seconds between each reinforcement spawn attempt.")]
        public float SpawnIntervalSeconds;
        
        [Tooltip("Maximum alive reinforcements allowed concurrently.")]
        public int MaxConcurrentSpawns;
        
        [Tooltip("Minimum distance to any player required to pick a spawn point.")]
        public float MinDistanceToPlayer;

        public static ReinforcementPolicy None => new ReinforcementPolicy
        {
            Budget = 0,
            SpawnIntervalSeconds = 0f,
            MaxConcurrentSpawns = 0,
            MinDistanceToPlayer = 0f
        };
    }
}
