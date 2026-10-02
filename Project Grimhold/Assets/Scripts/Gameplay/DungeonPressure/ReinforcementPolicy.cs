using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Spawning
{
    [Serializable]
    public struct ReinforcementPolicy
    {
        [Tooltip("Maximum active reinforcement threat allowed concurrently.")]
        [FormerlySerializedAs("MaxConcurrentSpawns")]
        public int PopulationBudget;
        
        [Tooltip("Seconds between each evaluation of the spawn conditions.")]
        [FormerlySerializedAs("SpawnIntervalSeconds")]
        public float EvaluationIntervalSeconds;
        
        [Tooltip("Minimum seconds required between successful generation of enemies.")]
        public float MinSecondsBetweenSpawns;
        
        [Tooltip("Maximum number of enemies to generate in a single evaluation attempt.")]
        public int MaxSpawnsPerAttempt;
        
        [Tooltip("Minimum distance to any player required to pick a spawn point.")]
        public float MinDistanceToPlayer;

        public static ReinforcementPolicy None => new ReinforcementPolicy
        {
            PopulationBudget = 0,
            EvaluationIntervalSeconds = 0f,
            MinSecondsBetweenSpawns = 0f,
            MaxSpawnsPerAttempt = 0,
            MinDistanceToPlayer = 0f
        };
    }
}
