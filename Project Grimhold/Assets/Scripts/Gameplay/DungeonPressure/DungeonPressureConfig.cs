using UnityEngine;

[CreateAssetMenu(fileName = "DungeonPressureConfig", menuName = "Grimhold/Gameplay/Dungeon Pressure Config")]
public sealed class DungeonPressureConfig : ScriptableObject
{
    [Tooltip("Total duration of the expedition in seconds. Provisory default.")]
    [SerializeField] private int _totalDurationSeconds = 900;
    
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
    [SerializeField] private Spawning.ReinforcementPolicy _collapsePolicy = new Spawning.ReinforcementPolicy { PopulationBudget = 8, EvaluationIntervalSeconds = 3f, MinSecondsBetweenSpawns = 0f, MaxSpawnsPerAttempt = 2, MinDistanceToPlayer = 10f };

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
            
            if (phase == DungeonPressurePhase.Normal && policy.PopulationBudget > 0)
            {
                error = $"Policy for phase {phase} must have PopulationBudget = 0 (reinforcements disabled).";
                return false;
            }

            if (phase == DungeonPressurePhase.Collapse && policy.PopulationBudget <= 0)
            {
                error = $"Policy for phase {phase} must have PopulationBudget > 0 (collapse applies maximum PvE pressure).";
                return false;
            }

            if (policy.PopulationBudget > _maxGlobalEnemies)
            {
                error = $"Policy for phase {phase} has PopulationBudget ({policy.PopulationBudget}) exceeding MaxGlobalEnemies ({_maxGlobalEnemies}).";
                return false;
            }

            if (policy.MaxSpawnsPerAttempt > policy.PopulationBudget && policy.PopulationBudget > 0)
            {
                error = $"Policy for phase {phase} has MaxSpawnsPerAttempt ({policy.MaxSpawnsPerAttempt}) exceeding PopulationBudget ({policy.PopulationBudget}).";
                return false;
            }

            if (policy.MinSecondsBetweenSpawns < 0f)
            {
                error = $"Policy for phase {phase} has MinSecondsBetweenSpawns < 0.";
                return false;
            }

            if (policy.MinDistanceToPlayer < 0f)
            {
                error = $"Policy for phase {phase} has MinDistanceToPlayer < 0.";
                return false;
            }

            if (policy.PopulationBudget > 0)
            {
                if (policy.EvaluationIntervalSeconds <= 0f)
                {
                    error = $"Policy for phase {phase} has PopulationBudget > 0 but EvaluationIntervalSeconds is <= 0.";
                    return false;
                }
                if (policy.MaxSpawnsPerAttempt < 1)
                {
                    error = $"Policy for phase {phase} has PopulationBudget > 0 but MaxSpawnsPerAttempt is < 1.";
                    return false;
                }
            }
        }

        var reinPolicy = GetPolicy(DungeonPressurePhase.Reinforcements);
        var critPolicy = GetPolicy(DungeonPressurePhase.CriticalPressure);

        if (critPolicy.PopulationBudget < reinPolicy.PopulationBudget)
        {
            error = "CriticalPressure PopulationBudget cannot be less than Reinforcements PopulationBudget.";
            return false;
        }

        if (critPolicy.PopulationBudget > 0 && reinPolicy.PopulationBudget > 0)
        {
            if (critPolicy.EvaluationIntervalSeconds > reinPolicy.EvaluationIntervalSeconds)
            {
                error = "CriticalPressure EvaluationIntervalSeconds cannot be greater than Reinforcements EvaluationIntervalSeconds (Critical should be more frequent).";
                return false;
            }
            if (critPolicy.MaxSpawnsPerAttempt < reinPolicy.MaxSpawnsPerAttempt)
            {
                error = "CriticalPressure MaxSpawnsPerAttempt cannot be less than Reinforcements MaxSpawnsPerAttempt.";
                return false;
            }
        }

        var collapsePolicy = GetPolicy(DungeonPressurePhase.Collapse);

        if (collapsePolicy.PopulationBudget < critPolicy.PopulationBudget)
        {
            error = "Collapse PopulationBudget cannot be less than CriticalPressure PopulationBudget.";
            return false;
        }

        if (critPolicy.PopulationBudget > 0)
        {
            if (collapsePolicy.EvaluationIntervalSeconds > critPolicy.EvaluationIntervalSeconds)
            {
                error = "Collapse EvaluationIntervalSeconds cannot be greater than CriticalPressure EvaluationIntervalSeconds (Collapse should be more frequent).";
                return false;
            }
            if (collapsePolicy.MaxSpawnsPerAttempt < critPolicy.MaxSpawnsPerAttempt)
            {
                error = "Collapse MaxSpawnsPerAttempt cannot be less than CriticalPressure MaxSpawnsPerAttempt.";
                return false;
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
