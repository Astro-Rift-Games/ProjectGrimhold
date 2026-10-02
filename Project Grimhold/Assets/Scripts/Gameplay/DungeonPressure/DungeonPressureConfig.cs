using UnityEngine;

[CreateAssetMenu(fileName = "DungeonPressureConfig", menuName = "Grimhold/Gameplay/Dungeon Pressure Config")]
public sealed class DungeonPressureConfig : ScriptableObject
{
    [Tooltip("Total duration of the expedition in seconds. Provisory default.")]
    [SerializeField] private int _totalDurationSeconds = 600;
    
    [Tooltip("Remaining seconds when Reinforcements phase begins.")]
    [SerializeField] private int _reinforcementsThresholdSeconds = 300;
    
    [Tooltip("Remaining seconds when Critical Pressure phase begins.")]
    [SerializeField] private int _criticalPressureThresholdSeconds = 120;

    [Tooltip("Global maximum number of active enemies allowed at any given time.")]
    [SerializeField] private int _maxGlobalEnemies = 40;

    public int TotalDurationSeconds => _totalDurationSeconds;
    public int ReinforcementsThresholdSeconds => _reinforcementsThresholdSeconds;
    public int CriticalPressureThresholdSeconds => _criticalPressureThresholdSeconds;
    public int MaxGlobalEnemies => _maxGlobalEnemies;

    [Header("Phase Policies")]
    [SerializeField] private Spawning.ReinforcementPolicy _normalPolicy = new Spawning.ReinforcementPolicy { PopulationBudget = 0, EvaluationIntervalSeconds = 10f, MinSecondsBetweenSpawns = 0f, MaxSpawnsPerAttempt = 1, MinDistanceToPlayer = 20f };
    [SerializeField] private Spawning.ReinforcementPolicy _reinforcementsPolicy = new Spawning.ReinforcementPolicy { PopulationBudget = 2, EvaluationIntervalSeconds = 15f, MinSecondsBetweenSpawns = 0f, MaxSpawnsPerAttempt = 1, MinDistanceToPlayer = 15f };
    [SerializeField] private Spawning.ReinforcementPolicy _criticalPressurePolicy = new Spawning.ReinforcementPolicy { PopulationBudget = 5, EvaluationIntervalSeconds = 5f, MinSecondsBetweenSpawns = 0f, MaxSpawnsPerAttempt = 1, MinDistanceToPlayer = 10f };
    [SerializeField] private Spawning.ReinforcementPolicy _collapsePolicy = Spawning.ReinforcementPolicy.None;

    public Spawning.ReinforcementPolicy GetPolicy(DungeonPressurePhase phase)
    {
        return phase switch
        {
            DungeonPressurePhase.Normal => _normalPolicy,
            DungeonPressurePhase.Reinforcements => _reinforcementsPolicy,
            DungeonPressurePhase.CriticalPressure => _criticalPressurePolicy,
            DungeonPressurePhase.Collapse => _collapsePolicy,
            _ => Spawning.ReinforcementPolicy.None
        };
    }

    public bool Validate(out string error)
    {
        if (_totalDurationSeconds <= 0)
        {
            error = "TotalDurationSeconds must be greater than 0.";
            return false;
        }
        if (_criticalPressureThresholdSeconds <= 0)
        {
            error = "CriticalPressureThresholdSeconds must be greater than 0.";
            return false;
        }
        if (_reinforcementsThresholdSeconds <= _criticalPressureThresholdSeconds)
        {
            error = "ReinforcementsThresholdSeconds must be strictly greater than CriticalPressureThresholdSeconds.";
            return false;
        }
        if (_totalDurationSeconds <= _reinforcementsThresholdSeconds)
        {
            error = "TotalDurationSeconds must be strictly greater than ReinforcementsThresholdSeconds.";
            return false;
        }
        if (_maxGlobalEnemies <= 0)
        {
            error = "MaxGlobalEnemies must be greater than 0.";
            return false;
        }

        var phases = new[] { DungeonPressurePhase.Normal, DungeonPressurePhase.Reinforcements, DungeonPressurePhase.CriticalPressure, DungeonPressurePhase.Collapse };
        foreach (var phase in phases)
        {
            var policy = GetPolicy(phase);
            if (policy.PopulationBudget > 0)
            {
                if (policy.EvaluationIntervalSeconds <= 0)
                {
                    error = $"Policy for phase {phase} has PopulationBudget > 0 but EvaluationIntervalSeconds is <= 0.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_totalDurationSeconds <= 0) _totalDurationSeconds = 1;
        if (_reinforcementsThresholdSeconds >= _totalDurationSeconds) _reinforcementsThresholdSeconds = _totalDurationSeconds - 1;
        if (_criticalPressureThresholdSeconds >= _reinforcementsThresholdSeconds) _criticalPressureThresholdSeconds = _reinforcementsThresholdSeconds - 1;
        if (_criticalPressureThresholdSeconds <= 0) _criticalPressureThresholdSeconds = 1;
    }
#endif
}
